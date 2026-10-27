using System.Net.WebSockets;
using System.Text.Json;
using OBSStudioClient.Enums;
using OBSStudioClient.Tests.TestSupport;
using OBSStudioClient.TestApp.Catalog;
using OBSStudioClient.TestApp.Views;

namespace OBSStudioClient.TestApp;

/// <summary>
/// The part of the smoke test that goes the whole way: the app connects, through its own Connection page, to a simulated OBS
/// Studio (the fake server of the tests, linked into the app for this), sends requests through the Explorer, sends a batch,
/// receives events and shows everything that happened in the Log. It is what shows that the pages, the session, the client and
/// the wire work together.
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
            // The pages were shown with sample events and log entries on them: start the real test from a clean slate, so that
            // what it checks is what the connection produced.
            App.Session.Log.Clear();
            App.Session.Observations.Clear();
            App.Session.Batch.Clear();
            foreach (var tag in new[] { "events", "log" })
            {
                window.Navigate(tag);
                await Task.Delay(300);
                (window.CurrentPage as EventsPage)?.Reset();
                (window.CurrentPage as LogPage)?.Reset();
            }

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
                await BatchAsync(window, explorer, folder);
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

        // The method hands back the value of the one field of the response: that field is what is compared.
        await explorer.SendForSmokeTest(RequestCatalog.Find("GetStudioModeEnabled")!);
        Check(explorer.Problem is null && explorer.TypedResult.Trim() == "false", "GetStudioModeEnabled returned false");
        Check(
            explorer.ComparisonResult.Contains("the member studioModeEnabled of the response data", StringComparison.Ordinal)
                && explorer.ComparisonResult.Contains("Identical:", StringComparison.Ordinal),
            "the comparison of a response with one field compares that field with what the client returned");
    }

    private static async Task BatchAsync(MainWindow window, ExplorerPage explorer, string folder)
    {
        // Four requests, the second of which OBS Studio refuses. GetHotkeyList is only ever answered inside the batch.
        Check(explorer.AddToBatchForSmokeTest(RequestCatalog.Find("GetVersion")!), "GetVersion was put in the batch");
        Check(explorer.AddToBatchForSmokeTest(RequestCatalog.Find("SetCurrentProgramScene")!), "SetCurrentProgramScene was put in the batch");
        Check(explorer.AddToBatchForSmokeTest(RequestCatalog.Find("Sleep")!, "sleepMillis"), "a Sleep was put in the batch");
        Check(explorer.AddToBatchForSmokeTest(RequestCatalog.Find("GetHotkeyList")!), "GetHotkeyList was put in the batch");
        Check(!explorer.AddToBatchForSmokeTest(RequestCatalog.Find("SetInputVolumeDb")!), "a shortcut of the client cannot be put in the batch");
        Check(explorer.AddToBatchForSmokeTest(RequestCatalog.Find("Sleep")!) is false && explorer.Problem is not null, "a Sleep of both milliseconds and frames is refused");
        Check(App.Session.Batch.Count == 4, "the batch holds the four requests that were accepted");

        window.Navigate("batch");
        await Task.Delay(500);
        if (window.CurrentPage is not BatchPage batch)
        {
            Record("The batch page was not shown.", isProblem: true);
            return;
        }

        await CaptureAsync(window, folder, "live-batch");

        batch.Configure(RequestBatchExecutionType.SerialRealtime, haltOnFailure: false);
        await batch.SendForSmokeTest();
        Check(batch.Problem is null, "the batch was answered");
        Check(batch.Status.StartsWith("OBS Studio ran 4 of 4 requests, 1 failed", StringComparison.Ordinal), "OBS Studio ran the four requests of the batch, and one of them failed");
        Check(batch.TypedResult.Contains("31.0.2", StringComparison.Ordinal), "the batch shows what the client returned for GetVersion");
        Check(batch.TypedResult.Contains("ReplayBuffer.Save", StringComparison.Ordinal), "the batch shows what the client returned for GetHotkeyList");
        Check(batch.TypedResult.Contains("\"codeName\": \"ResourceNotFound\"", StringComparison.Ordinal), "the batch shows the request that OBS Studio refused");
        Check(batch.RawResult.Contains("\"op\": 9", StringComparison.Ordinal), "the batch shows the answer of OBS Studio");
        Check(batch.ComparisonResult.Contains("$.futureField (object)", StringComparison.Ordinal), "the batch compares the data of GetVersion");
        Check(
            App.Session.Observations.ToJson(null, null).Contains("\"GetHotkeyList\"", StringComparison.Ordinal),
            "the shape of a response that came inside a batch was observed");
        await Task.Delay(300);
        await CaptureAsync(window, folder, "live-batch-result");
        batch.SelectResultTab(2);
        await Task.Delay(300);
        await CaptureAsync(window, folder, "live-batch-comparison");

        batch.Configure(RequestBatchExecutionType.SerialRealtime, haltOnFailure: true);
        await batch.SendForSmokeTest();
        Check(
            batch.Problem is null && batch.Status.StartsWith("OBS Studio ran 2 of 4 requests, 1 failed", StringComparison.Ordinal),
            "OBS Studio stopped at the request that failed, because the client sent haltOnFailure");
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
                var op = root.GetProperty("op").GetInt32();
                if (op == 6)
                {
                    var data = root.GetProperty("d");
                    var requestType = data.GetProperty("requestType").GetString()!;
                    var requestId = data.GetProperty("requestId").GetString()!;
                    await Serialized(() => RespondAsync(server, requestType, requestId));
                }
                else if (op == 8)
                {
                    var batch = root.GetProperty("d");
                    await Serialized(() => RespondToBatchAsync(server, batch));
                }
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

    // What the simulated OBS Studio answers to a request: a status code (100 is success), a comment, and the response data.
    private sealed record Reply(int Code, string? Comment, object? Data)
    {
        public bool Succeeded => this.Code == 100;
    }

    private static Reply ReplyTo(string requestType) => requestType switch
    {
        "GetVersion" => new Reply(100, null, new
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
        "GetSceneList" => new Reply(100, null, new
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
        "GetInputList" => new Reply(100, null, new
        {
            inputs = new[]
            {
                new { inputKind = "wasapi_input_capture", inputName = "Mic/Aux", inputUuid = Microphone, unversionedInputKind = "wasapi_input_capture" },
            },
        }),
        "SetCurrentProgramScene" => new Reply(600, "No scene was found by the name of name.", null),
        "GetStudioModeEnabled" => new Reply(100, null, new { studioModeEnabled = false }),
        "GetHotkeyList" => new Reply(100, null, new { hotkeys = new[] { "ReplayBuffer.Save", "OBSBasic.StartStreaming" } }),
        _ => new Reply(100, null, null),
    };

    private static Task RespondAsync(FakeObsWebSocketServer server, string requestType, string requestId)
    {
        var reply = ReplyTo(requestType);
        return reply.Succeeded
            ? server.SendRequestSuccessAsync(requestType, requestId, reply.Data)
            : server.SendRequestErrorAsync(requestType, requestId, reply.Code, reply.Comment);
    }

    // Like obs-websocket: the requests run in order, and with haltOnFailure the batch stops at the first one that fails, so that
    // the answer holds only the requests that ran.
    private static Task RespondToBatchAsync(FakeObsWebSocketServer server, JsonElement batch)
    {
        var haltOnFailure = batch.TryGetProperty("haltOnFailure", out var halt) && halt.ValueKind == JsonValueKind.True;
        var results = new List<object>();
        foreach (var request in batch.GetProperty("requests").EnumerateArray())
        {
            var requestType = request.GetProperty("requestType").GetString()!;
            var reply = ReplyTo(requestType);
            results.Add(new
            {
                requestType,
                requestId = request.GetProperty("requestId").GetString(),
                requestStatus = new { result = reply.Succeeded, code = reply.Code, comment = reply.Comment },
                responseData = reply.Data,
            });
            if (haltOnFailure && !reply.Succeeded)
            {
                break;
            }
        }

        return server.SendRawAsync(JsonSerializer.Serialize(new { op = 9, d = new { requestId = batch.GetProperty("requestId").GetString(), results } }));
    }
}
