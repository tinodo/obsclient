using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using OBSStudioClient.Messages;
using OBSStudioClient.TestApp.Catalog;
using OBSStudioClient.TestApp.Services;
using OBSStudioClient.TestApp.Views;

namespace OBSStudioClient.TestApp;

/// <summary>
/// Started as <c>OBSClient.TestApp.exe --smoke-test &lt;log&gt;</c>, the app opens every page, builds, fills in and reads the
/// editors of every request, writes what happened to the log, takes a picture of each page next to the log, and ends with exit
/// code 0 (or 1 when something went wrong). CI runs it after every build: the XAML of an app is only read when the app runs, so
/// this is where a mistake in it shows, and the pictures are how the design can be looked at without running the app.
/// </summary>
internal static partial class SmokeTest
{
    private static readonly StringBuilder Report = new();
    private static bool hasProblem;
    private static string? writtenTo;

    /// <summary>Determines whether the app was asked to run the smoke test.</summary>
    /// <param name="path">Where to write the log.</param>
    public static bool IsRequested(out string path)
    {
        var arguments = Environment.GetCommandLineArgs();
        var index = Array.IndexOf(arguments, "--smoke-test");
        path = index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : string.Empty;
        return index >= 0;
    }

    /// <summary>Adds a line to the log; a problem makes the smoke test fail.</summary>
    public static void Record(string message, bool isProblem = false)
    {
        lock (Report)
        {
            Report.AppendLine(message);
            hasProblem |= isProblem;

            // The log is written when the checks are done: what the app reports while it closes is added to it.
            if (writtenTo is not null)
            {
                try
                {
                    File.AppendAllText(writtenTo, "after the checks: " + message + Environment.NewLine);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    // Nothing more can be done about it.
                }
            }
        }
    }

    /// <summary>Runs the smoke test and ends the app.</summary>
    public static async Task RunAsync(MainWindow window, string path)
    {
        // A crash while the app closes leaves nothing behind but what these report.
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Record($"Unhandled exception in the app domain: {e.ExceptionObject}", isProblem: true);
        TaskScheduler.UnobservedTaskException += (_, e) => Record($"Unobserved task exception: {e.Exception}");

        var folder = Path.GetDirectoryName(Path.GetFullPath(path.Length > 0 ? path : "smoke-test.log")) ?? ".";
        try
        {
            Directory.CreateDirectory(folder);
        }
        catch (Exception exception)
        {
            Record($"The folder of the log could not be made: {exception.Message}", isProblem: true);
        }

        // A smoke test that hangs would hold up CI for hours: it ends itself, as a failure.
        var watchdog = window.DispatcherQueue.CreateTimer();
        watchdog.Interval = TimeSpan.FromMinutes(4);
        watchdog.IsRepeating = false;
        watchdog.Tick += (_, _) =>
        {
            Record("The smoke test did not finish in four minutes.", isProblem: true);
            Finish(path);
        };
        watchdog.Start();

        try
        {
            await Task.Delay(1500);
            await ShowAsync(window, "connection", typeof(ConnectionPage), folder);

            // An event that was raised before the Events page was ever opened has to be on it when it is opened.
            RaiseEarlyEvent();
            await ShowAsync(window, "events", typeof(EventsPage), folder, prepare: RaiseSampleEvents, inspect: () => (window.CurrentPage as EventsPage)?.SelectFirst());
            if (window.CurrentPage is EventsPage opened)
            {
                Check(opened.Shown.Any(entry => entry.Name == "StudioModeStateChanged"), "the Events page shows an event that was raised before the page was opened");
            }

            await ShowAsync(window, "log", typeof(LogPage), folder, prepare: WriteSampleLog, inspect: () => (window.CurrentPage as LogPage)?.SelectFirst());

            await ShowAsync(window, "explorer", typeof(ExplorerPage), folder);
            if (window.CurrentPage is ExplorerPage explorer)
            {
                CheckEditors(explorer);
                var forms = new[]
                {
                    ("SetInputSettings", "explorer-input"),
                    ("SetSceneItemTransform", "explorer-transform"),
                    ("SetInputMute", "explorer-boolean"),
                    ("TriggerHotkeyByKeySequence", "explorer-enum"),
                    ("OpenSourceProjectorOnMonitor", "explorer-source"),
                    ("GetVersion", "explorer-no-parameters"),
                };
                foreach (var (name, picture) in forms)
                {
                    if (RequestCatalog.Find(name) is { } request)
                    {
                        explorer.OpenRequest(request);
                        await Task.Delay(400);
                        await CaptureAsync(window, folder, picture);
                    }
                    else
                    {
                        Record($"{name} is not in the catalog.", isProblem: true);
                    }
                }
            }

            await ShowAsync(window, "batch", typeof(BatchPage), folder);

            await EndToEndAsync(window, folder);
        }
        catch (Exception exception)
        {
            Record(exception.ToString(), isProblem: true);
        }

        watchdog.Stop();
        Finish(path);
    }

    private static async Task ShowAsync(MainWindow window, string tag, Type page, string folder, Action? prepare = null, Action? inspect = null)
    {
        window.Navigate(tag);
        await Task.Delay(500);
        if (window.CurrentPage?.GetType() != page)
        {
            Record($"The {tag} page was not shown.", isProblem: true);
            return;
        }

        Record($"opened the {tag} page");
        prepare?.Invoke();
        await Task.Delay(500);
        inspect?.Invoke();
        await Task.Delay(300);
        await CaptureAsync(window, folder, tag);
    }

    private static void CheckEditors(ExplorerPage explorer)
    {
        var built = 0;
        foreach (var request in RequestCatalog.All)
        {
            try
            {
                var arguments = explorer.FillAndCollect(request);
                if (request.AddToBatch is { } addToBatch && request.Name != "Sleep")
                {
                    addToBatch(new RequestBatchMessage(), arguments);
                }

                built++;
            }
            catch (Exception exception)
            {
                Record($"{request.Name}: {exception.GetType().Name}: {exception.Message}", isProblem: true);
            }
        }

        Record($"built, filled in and read the editors of {built} of {RequestCatalog.All.Count} requests");
    }

    private static async Task CaptureAsync(MainWindow window, string folder, string name)
    {
        var root = window.Content as Microsoft.UI.Xaml.Controls.Panel;
        var original = root?.Background;
        try
        {
            if (window.Content is UIElement content)
            {
                // The picture holds what the app paints, and what the Mica backdrop shows is not painted by the app: without a
                // background the picture would be transparent there.
                if (root is not null
                    && Application.Current.Resources.TryGetValue("SolidBackgroundFillColorBaseBrush", out var brush)
                    && brush is Microsoft.UI.Xaml.Media.Brush solid)
                {
                    root.Background = solid;
                }

                await ScreenCapture.SaveAsync(content, Path.Combine(folder, $"smoke-test-{name}.png"));
                Record($"took a picture of {name}");
            }
        }
        catch (Exception exception)
        {
            // A picture is a convenience: its failure is reported, but does not fail the smoke test.
            Record($"no picture of {name}: {exception.GetType().Name}: {exception.Message}");
        }
        finally
        {
            if (root is not null)
            {
                root.Background = original;
            }
        }
    }

    private static void RaiseEarlyEvent() =>
        App.Session.RaiseForSmokeTest(new SessionEvent(
            DateTimeOffset.Now,
            "StudioModeStateChanged",
            "Ui",
            "{\n  \"StudioModeEnabled\": true\n}",
            "{\"d\":{\"eventData\":{\"studioModeEnabled\":true},\"eventIntent\":1024,\"eventType\":\"StudioModeStateChanged\"},\"op\":5}"));

    private static void RaiseSampleEvents()
    {
        var session = App.Session;
        session.RaiseForSmokeTest(new SessionEvent(
            DateTimeOffset.Now,
            "CurrentProgramSceneChanged",
            "Scenes",
            "{\n  \"SceneName\": \"Game\",\n  \"SceneUuid\": \"3f2504e0-4f89-11d3-9a0c-0305e82c3301\"\n}",
            "{\"d\":{\"eventData\":{\"sceneName\":\"Game\",\"sceneUuid\":\"3f2504e0-4f89-11d3-9a0c-0305e82c3301\"},\"eventIntent\":4,\"eventType\":\"CurrentProgramSceneChanged\"},\"op\":5}"));
        session.RaiseForSmokeTest(new SessionEvent(
            DateTimeOffset.Now,
            "InputMuteStateChanged",
            "Inputs",
            "{\n  \"InputName\": \"Mic/Aux\",\n  \"InputMuted\": true\n}",
            "{\"d\":{\"eventData\":{\"inputName\":\"Mic/Aux\",\"inputUuid\":\"5b7e1d1c-8a49-4c1b-a1b7-0a4f3f9f2c11\",\"inputMuted\":true},\"eventIntent\":8,\"eventType\":\"InputMuteStateChanged\"},\"op\":5}"));
        session.RaiseForSmokeTest(new SessionEvent(
            DateTimeOffset.Now,
            "RecordStateChanged",
            "Outputs",
            "{\n  \"OutputActive\": true,\n  \"OutputState\": \"OBS_WEBSOCKET_OUTPUT_STARTED\"\n}",
            null));
    }

    private static void WriteSampleLog()
    {
        var log = App.Session.Log;
        log.Note(LogLevel.Debug, "Sending: {\"d\":{\"requestId\":\"1\",\"requestType\":\"GetVersion\"},\"op\":6}");
        log.Note(LogLevel.Debug, "Received: {\"d\":{\"requestId\":\"1\",\"requestStatus\":{\"code\":100,\"result\":true},\"requestType\":\"GetVersion\",\"responseData\":{\"obsVersion\":\"31.0.0\",\"obsWebSocketVersion\":\"5.7.6\"}},\"op\":7}");
        log.Note(LogLevel.Debug, "Sending: {\"d\":{\"requestData\":{\"sceneName\":\"Missing\"},\"requestId\":\"2\",\"requestType\":\"SetCurrentProgramScene\"},\"op\":6}");
        log.Note(LogLevel.Debug, "Received: {\"d\":{\"requestId\":\"2\",\"requestStatus\":{\"code\":600,\"comment\":\"No scene was found by the name of Missing.\",\"result\":false},\"requestType\":\"SetCurrentProgramScene\"},\"op\":7}");
        log.Note(LogLevel.Warning, "The response to request 3 could not be read.");
        log.Note(LogLevel.Information, "The connection was closed: AuthenticationFailed");
    }

    private static void Finish(string path)
    {
        string text;
        bool failed;
        lock (Report)
        {
            text = Report.ToString();
            failed = hasProblem;
        }

        try
        {
            if (path.Length > 0)
            {
                File.WriteAllText(path, text + (failed ? "FAILED" : "OK") + Environment.NewLine);
                lock (Report)
                {
                    writtenTo = path;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            failed = true;
        }
        finally
        {
            Environment.ExitCode = failed ? 1 : 0;
            Application.Current.Exit();
        }
    }
}
