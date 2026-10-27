using System.Text;
using Microsoft.UI.Xaml;
using OBSStudioClient.Messages;
using OBSStudioClient.TestApp.Catalog;
using OBSStudioClient.TestApp.Views;

namespace OBSStudioClient.TestApp;

/// <summary>
/// Started as <c>OBSClient.TestApp.exe --smoke-test &lt;log&gt;</c>, the app opens every page, builds, fills in and reads the
/// editors of every request, writes what happened to the log, and ends with exit code 0 (or 1 when something went wrong).
/// CI runs it after every build: the XAML of an app is only read when the app runs, so this is where a mistake in it shows.
/// </summary>
internal static class SmokeTest
{
    private static readonly StringBuilder Report = new();
    private static bool hasProblem;

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
        }
    }

    /// <summary>Runs the smoke test and ends the app.</summary>
    public static async Task RunAsync(MainWindow window, string path)
    {
        try
        {
            var pages = new (string Tag, Type Page)[]
            {
                ("connection", typeof(ConnectionPage)),
                ("events", typeof(EventsPage)),
                ("log", typeof(LogPage)),
                ("explorer", typeof(ExplorerPage)),
            };

            foreach (var (tag, page) in pages)
            {
                window.Navigate(tag);
                await Task.Delay(400);
                if (window.CurrentPage?.GetType() != page)
                {
                    Record($"The {tag} page was not shown.", isProblem: true);
                    continue;
                }

                Record($"opened the {tag} page");
            }

            if (window.CurrentPage is ExplorerPage explorer)
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
        }
        catch (Exception exception)
        {
            Record(exception.ToString(), isProblem: true);
        }

        Finish(path);
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

        if (path.Length > 0)
        {
            File.WriteAllText(path, text + (failed ? "FAILED" : "OK") + Environment.NewLine);
        }

        Environment.ExitCode = failed ? 1 : 0;
        Application.Current.Exit();
    }
}
