namespace OBSStudioClient.Tests
{
    using OBSStudioClient.Events;
    using OBSStudioClient.Tests.TestSupport;
    using Xunit;

    /// <summary>
    /// Verifies event dispatch, including the resilience fix for event types this client version does not (yet)
    /// recognize. See https://github.com/tinodo/obsclient/issues/12, and the new Canvases category added in
    /// obs-websocket 5.7.0.
    /// </summary>
    public class EventHandlingTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private static async Task<(ObsClient Client, FakeObsWebSocketServer Server)> ConnectAsync(FakeObsWebSocketServer server)
        {
            server.Start();
            var client = new ObsClient
            {
                // The default 500ms is tight for a loaded CI machine running both the net8.0 and net10.0 test
                // TFMs in parallel; these tests care about correctness, not about exercising the default timeout.
                RequestTimeout = 5000,
            };
            var connectTask = client.ConnectAsync(hostname: "127.0.0.1", port: server.Port);
            await server.HandshakeAsync().WithTimeoutAsync(Timeout);
            await connectTask.WithTimeoutAsync(Timeout);
            return (client, server);
        }

        [Fact]
        public async Task KnownEventType_InvokesCorrespondingClientEvent()
        {
            await using var server = new FakeObsWebSocketServer();
            var (client, _) = await ConnectAsync(server);
            using var _1 = client;

            var awaiter = new EventAwaiter();
            client.ExitStarted += awaiter.Handler;

            await server.SendEventAsync("ExitStarted");

            await awaiter.Task.WithTimeoutAsync(Timeout, "ExitStarted handler was not invoked.");
        }

        [Fact]
        public async Task UnrecognizedEventType_DoesNotThrowAndConnectionStaysUsable()
        {
            // Regression test: obs-websocket can add new event types (this client once crashed on exactly this,
            // see https://github.com/tinodo/obsclient/issues/12). An event type unknown to this client version
            // must be ignored, not tear down the connection.
            await using var server = new FakeObsWebSocketServer();
            var (client, _) = await ConnectAsync(server);
            using var _1 = client;

            await server.SendEventAsync("SomeFutureEventThisClientDoesNotKnowAbout", new { someField = 1 });

            // Prove the receive loop is still alive and the connection is still healthy by completing a
            // normal request/response round trip right after the unrecognized event.
            var requestTask = client.GetCanvasList();
            var (requestType, requestId, _) = await server.ReceiveRequestAsync(Timeout);
            Assert.Equal("GetCanvasList", requestType);
            await server.SendRequestSuccessAsync(requestType, requestId, new { canvases = Array.Empty<object>() });

            var canvases = await requestTask.WithTimeoutAsync(Timeout);
            Assert.Empty(canvases);
            Assert.Equal(Enums.ConnectionState.Connected, client.ConnectionState);
        }

        [Fact]
        public async Task CanvasCreated_InvokesCanvasCreatedEventWithExpectedFields()
        {
            await using var server = new FakeObsWebSocketServer();
            var (client, _) = await ConnectAsync(server);
            using var _1 = client;

            var awaiter = new EventAwaiter<CanvasCreatedEventArgs>();
            client.CanvasCreated += awaiter.Handler;

            var canvasUuid = Guid.NewGuid();
            await server.SendEventAsync("CanvasCreated", new { canvasName = "Vertical Canvas", canvasUuid });

            var args = await awaiter.Task.WithTimeoutAsync(Timeout, "CanvasCreated handler was not invoked.");

            Assert.Equal("Vertical Canvas", args.CanvasName);
            Assert.Equal(canvasUuid, args.CanvasUuid);
        }
    }
}
