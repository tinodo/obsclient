using System.Net.WebSockets;
using OBSStudioClient.Tests.TestSupport;
using OBSStudioClient.TestApp.Catalog;
using OBSStudioClient.TestApp.Views;

namespace OBSStudioClient.TestApp;

/// <summary>
/// The part of the smoke test that goes the whole way: the app connects, through its own Connection page, to a simulated OBS
/// Studio (the fake server of the tests, linked into the app for this), sends requests through the Explorer, receives events and
/// shows everything that happened in the Log. It is what shows that the pages, the session, the client and the wire work together.
/// </summary>
internal static partial class SmokeTest
{
    private static readonly Guid GameScene = Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301");
    private static readonly Guid ChatScene = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");
    private static readonly Guid Microphone = Guid.Parse("5b7e1d1c-8a49-4c1b-a1b7-0a4f3f9f2c11");

    // A WebSocket sends one message at a time: the answers to requests and the events of this test take turns.
    private static readonly SemaphoreSlim SendLock = new(1, 1);

    private static async Task EndToEndAsync(MainWindow window, string folder)
    {
        await using var server = new FakeObsWebSocketServer();
        server.Start();
        var stopping = false;
        Task? answering = null;

        try
        {
            window.Navigate("connection");
            await Task.Delay(300);
            if (window.CurrentPage is not ConnectionPage connection)
            {
                Record("The connection page was not shown for the end to end test.", isProblem: true);
                return;
            }

            var connect = connection.ConnectAsync("127.0.0.1", server.Port);
            await server.HandshakeAsync();
            answering = Task.Run(() => AnswerAsync(server, () => stopping));
            await connect;

            Check(App.Session.IsConnected, "the app connected to the simulated OBS Studio");
            Check(
                await WaitUntilAsync(() => App.Session.ObsVersion is not null && App.Session.Scenes.Count == 2 && App.Session.Inputs.Count == 1),
                "the app read the version, the scenes and the inputs by itself");
            await Task.Delay(300);
            await CaptureAsync(window, folder, "live-connection");

            window.Navigate("explorer");
            await Task.Delay(300);
            if (window.CurrentPage is ExplorerPage explorer)
            {
                await ExploreAsync(window, explorer, folder);
            }

            window.Navigate("events");
            await Task.Delay(300);
            await Serialized(() => server.SendEventAsync("CurrentProgramSceneChanged", new { sceneName = "Game", sceneUuid = GameScene }));
            await Serialized(() => server.SendEventAsync("InputMuteStateChanged", new { inputName = "Mic/Aux", inputUuid = Microphone, inputMuted = true }));
            if (window.CurrentPage is EventsPage events)
            {
                Check(await WaitUntilAsync(() => events.Shown.Count >= 2), "the Events page shows the two events of OBS Studio");
                events.SelectFirst();
                await Task.Delay(300);
                var first = events.Shown.FirstOrDefault();
                Check(first is { RawJson: not null } && first.TypedJson.Contains("Game", StringComparison.Ordinal), "an event shows what the client made of it, and what OBS Studio sent");
                await CaptureAsync(window, folder, "live-events");
            }

            window.Navigate("log");
            await Task.Delay(800);
            if (window.CurrentPage is LogPage log)
            {
                Check(log.Shown.Any(entry => entry.Title == "Request GetVersion"), "the Log shows the request that was sent");
                Check(log.Shown.Any(entry => entry.Title == "Response GetVersion"), "the Log shows the response that was received");
                Check(log.Shown.Any(entry => entry.Failed), "the Log marks the response that failed");
                Check(log.Shown.Any(entry => entry.Title == "Event CurrentProgramSceneChanged"), "the Log shows the event that was received");
                await CaptureAsync(window, folder, "live-log");
            }

            Check(App.Session.Observations.Count >= 3, "the shapes of what OBS Studio sent were observed");

            App.Session.Disconnect();
            Check(await WaitUntilAsync(() => !App.Session.IsConnected), "the app disconnected");
        }
        finally
        {
            stopping = true;
            App.Session.Disconnect();
            if (answering is not null)
            {
                await Task.WhenAny(answering, Task.Delay(2000));
            }
        }
    }

    private static async Task ExploreAsync(MainWindow window, ExplorerPage explorer, string folder)
    {
        await explorer.SendForSmokeTest(RequestCatalog.Find("GetVersion")!);
        Check(explorer.Problem is null, "GetVersion succeeded");
        Check(explorer.TypedResult.Contains("31.0.2", StringComparison.Ordinal), "the result shows what the client returned");
        Check(explorer.RawResult.Contains("\"requestType\": \"GetVersion\"", StringComparison.Ordinal), "the result shows what OBS Studio sent");
        Check(
            explorer.ComparisonResult.Contains("$.futureField (object)", StringComparison.Ordinal),
            "the comparison finds a member that OBS Studio sent and the client does not return");
        await Task.Delay(300);
        await CaptureAsync(window, folder, "live-explorer-result");
        explorer.SelectResultTab(2);
        await Task.Delay(300);
        await CaptureAsync(window, folder, "live-explorer-comparison");

        await explorer.SendForSmokeTest(RequestCatalog.Find("SetCurrentProgramScene")!);
        Check(
            explorer.Problem is { } problem && problem.Contains("OBS Studio refused the request", StringComparison.Ordinal) && problem.Contains("ResourceNotFound", StringComparison.Ordinal),
            "a request that OBS Studio refuses is explained");
        await Task.Delay(300);
        await CaptureAsync(window, folder, "live-explorer-refused");

        await explorer.SendForSmokeTest(RequestCatalog.Find("GetSceneList")!);
        Check(explorer.Problem is null && explorer.TypedResult.Contains("Just Chatting", StringComparison.Ordinal), "GetSceneList returned the scenes");
        Check(explorer.ComparisonResult.StartsWith("What OBS Studio sent", StringComparison.Ordinal), "the comparison of GetSceneList was made");
    }

    private static void Check(bool condition, string what) => Record(condition ? $"ok: {what}" : $"FAILED: {what}", isProblem: !condition);

    private static async Task<bool> WaitUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(50);
        }

        return condition();
    }

    private static async Task AnswerAsync(FakeObsWebSocketServer server, Func<bool> stopping)
    {
        try
        {
            while (!stopping())
            {
                using var message = await server.ReceiveJsonAsync(TimeSpan.FromSeconds(120));
                var root = message.RootElement;
                if (root.GetProperty("op").GetInt32() != 6)
                {
                    continue;
                }

                var data = root.GetProperty("d");
                var requestType = data.GetProperty("requestType").GetString()!;
                var requestId = data.GetProperty("requestId").GetString()!;
                await Serialized(() => RespondAsync(server, requestType, requestId));
            }
        }
        catch (Exception exception) when (stopping() || exception is OperationCanceledException or InvalidOperationException or WebSocketException)
        {
            // The connection is being closed.
        }
    }

    private static async Task Serialized(Func<Task> send)
    {
        await SendLock.WaitAsync();
        try
        {
            await send();
        }
        finally
        {
            SendLock.Release();
        }
    }

    private static Task RespondAsync(FakeObsWebSocketServer server, string requestType, string requestId) => requestType switch
    {
        "GetVersion" => server.SendRequestSuccessAsync(requestType, requestId, new
        {
            obsVersion = "31.0.2",
            obsWebSocketVersion = "5.7.6",
            rpcVersion = 1,
            availableRequests = new[] { "GetVersion", "GetSceneList", "GetInputList" },
            supportedImageFormats = new[] { "png", "jpg" },
            platform = "windows",
            platformDescription = "Windows 11 Version 24H2",
            futureField = new { nested = 1 },
        }),
        "GetSceneList" => server.SendRequestSuccessAsync(requestType, requestId, new
        {
            currentProgramSceneName = "Game",
            currentProgramSceneUuid = GameScene,
            currentPreviewSceneName = (string?)null,
            currentPreviewSceneUuid = (Guid?)null,
            scenes = new[]
            {
                new { sceneIndex = 1, sceneName = "Just Chatting", sceneUuid = ChatScene },
                new { sceneIndex = 0, sceneName = "Game", sceneUuid = GameScene },
            },
        }),
        "GetInputList" => server.SendRequestSuccessAsync(requestType, requestId, new
        {
            inputs = new[]
            {
                new { inputKind = "wasapi_input_capture", inputName = "Mic/Aux", inputUuid = Microphone, unversionedInputKind = "wasapi_input_capture" },
            },
        }),
        "SetCurrentProgramScene" => server.SendRequestErrorAsync(requestType, requestId, 600, "No scene was found by the name of name."),
        _ => server.SendRequestSuccessAsync(requestType, requestId),
    };
}
