namespace OBSStudioClient.Tests.Unit
{
    using System.Text.Json;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Messages;
    using OBSStudioClient.Tests.TestSupport;

    /// <summary>
    /// The numbering of the messages of OBS Studio in the order of arrival, and the tap that reports it to the model. The model
    /// has to know in which order things arrived: the handlers of the events run later, one at a time, and a response completes its request at
    /// once, so a response can reach its caller before an event that arrived earlier.
    /// </summary>
    public class IngressTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        [Fact]
        public async Task TheStartOfAConnection_IsReported_BeforeConnectAsyncReturns()
        {
            await using var server = new FakeObsWebSocketServer();
            var tap = new TapRecorder();
            using var client = await ConnectAsync(server, tap.Add);

            var started = Assert.Single(tap.Items);
            Assert.Equal(IngressKind.SessionStarted, started.Kind);

            // The Hello message is number 1, the Identified message that completes the handshake number 2, and nothing came after it.
            Assert.Equal(2, started.Sequence);
            Assert.Equal(started.Sequence, client.LastIngress);
        }

        [Fact]
        public async Task EveryEvent_IsReported_InTheOrderOfArrival_EvenWhenNobodyListensToIt()
        {
            await using var server = new FakeObsWebSocketServer();
            var tap = new TapRecorder();
            using var client = await ConnectAsync(server, tap.Add);

            await server.SendEventAsync("InputMuteStateChanged", new { inputName = "Microphone", inputUuid = Guid.NewGuid(), inputMuted = true }, eventIntent: 8);
            await server.SendEventAsync("SceneCreated", SceneData("Gaming"), eventIntent: 4);
            await server.SendEventAsync("CurrentProgramSceneChanged", new { sceneName = "Gaming", sceneUuid = Guid.NewGuid() }, eventIntent: 4);

            Assert.True(await tap.WaitForAsync(items => items.Count(item => item.Kind == IngressKind.Event) == 3), "Not every event was reported.");
            var events = tap.Items.Where(item => item.Kind == IngressKind.Event).ToArray();

            Assert.Equal(
                new[] { EventType.InputMuteStateChanged, EventType.SceneCreated, EventType.CurrentProgramSceneChanged },
                events.Select(item => item.Event!.EventType).ToArray());
            Assert.Equal(new[] { events[0].Sequence, events[0].Sequence + 1, events[0].Sequence + 2 }, events.Select(item => item.Sequence).ToArray());
        }

        [Fact]
        public async Task AResponse_CarriesANumber_BetweenTheEventsThatArrivedAroundIt()
        {
            await using var server = new FakeObsWebSocketServer();
            var tap = new TapRecorder();
            using var client = await ConnectAsync(server, tap.Add);

            var request = client.SendRawRequestAsync(RequestType.GetStudioModeEnabled, null);
            var (requestType, requestId, _) = await server.ReceiveRequestAsync(Timeout);
            await server.SendEventAsync("SceneCreated", SceneData("Before"), eventIntent: 4);
            await server.SendRequestSuccessAsync(requestType, requestId, new { studioModeEnabled = false });
            await server.SendEventAsync("SceneCreated", SceneData("After"), eventIntent: 4);
            var response = await request.WithTimeoutAsync(Timeout);

            Assert.True(await tap.WaitForAsync(items => items.Count(item => item.Kind == IngressKind.Event) == 2), "Not every event was reported.");
            var events = tap.Items.Where(item => item.Kind == IngressKind.Event).ToArray();
            Assert.Equal(events[0].Sequence + 1, response.IngressSequence);
            Assert.Equal(response.IngressSequence + 1, events[1].Sequence);
        }

        [Fact]
        public async Task ABatchResponse_CarriesANumber_AndSoDoTheResponsesInIt()
        {
            await using var server = new FakeObsWebSocketServer();
            var tap = new TapRecorder();
            using var client = await ConnectAsync(server, tap.Add);
            var batch = new RequestBatchMessage();
            batch.AddGetStudioModeEnabledRequest();

            var sending = client.SendRawBatchAsync(batch);
            using var message = await server.ReceiveJsonAsync(Timeout);
            var requests = message.RootElement.GetProperty("d").GetProperty("requests");
            var results = new[]
            {
                new
                {
                    requestType = "GetStudioModeEnabled",
                    requestId = requests[0].GetProperty("requestId").GetString(),
                    requestStatus = new { result = true, code = 100 },
                    responseData = new { studioModeEnabled = false },
                },
            };
            await server.SendEventAsync("SceneCreated", SceneData("Before"), eventIntent: 4);
            await server.SendRawAsync(JsonSerializer.Serialize(new { op = 9, d = new { requestId = batch.RequestId, results } }));
            var response = await sending.WithTimeoutAsync(Timeout);

            Assert.True(await tap.WaitForAsync(items => items.Any(item => item.Kind == IngressKind.Event)), "The event was not reported.");
            var theEvent = tap.Items.Single(item => item.Kind == IngressKind.Event);
            Assert.Equal(theEvent.Sequence + 1, response.IngressSequence);
            Assert.Equal(response.IngressSequence, Assert.Single(response.Results).IngressSequence);
        }

        [Fact]
        public async Task ABatchResponseWithANullResult_IsReturnedAsItCame_AndDoesNotEndTheConnection()
        {
            await using var server = new FakeObsWebSocketServer();
            var tap = new TapRecorder();
            using var client = await ConnectAsync(server, tap.Add);
            var batch = new RequestBatchMessage();
            batch.AddGetStudioModeEnabledRequest();

            var sending = client.SendRawBatchAsync(batch);
            await server.ReceiveJsonAsync(Timeout);
            await server.SendRawAsync(JsonSerializer.Serialize(new { op = 9, d = new { requestId = batch.RequestId, results = new object?[] { null } } }));
            var response = await sending.WithTimeoutAsync(Timeout);

            Assert.Single(response.Results);
            Assert.Null(response.Results[0]);
            Assert.Equal(ConnectionState.Connected, client.ConnectionState);
            Assert.DoesNotContain(tap.Items, item => item.Kind == IngressKind.SessionEnded);
        }

        [Fact]
        public async Task ASlowHandlerOfTheApplication_DoesNotDelayTheTap()
        {
            await using var server = new FakeObsWebSocketServer();
            var tap = new TapRecorder();
            using var client = await ConnectAsync(server, tap.Add);
            using var release = new ManualResetEventSlim();
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var handled = 0;
            client.SceneCreated += (_, _) =>
            {
                Interlocked.Increment(ref handled);
                entered.TrySetResult();
                release.Wait(Timeout);
            };

            await server.SendEventAsync("SceneCreated", SceneData("One"), eventIntent: 4);
            await entered.Task.WithTimeoutAsync(Timeout);
            await server.SendEventAsync("SceneCreated", SceneData("Two"), eventIntent: 4);
            await server.SendEventAsync("SceneCreated", SceneData("Three"), eventIntent: 4);

            // The handler is still running its first event, so the other two wait for it: the tap has all three.
            Assert.True(await tap.WaitForAsync(items => items.Count(item => item.Kind == IngressKind.Event) == 3), "The tap waited for the handler.");
            Assert.Equal(1, Volatile.Read(ref handled));

            release.Set();
            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => Volatile.Read(ref handled) == 3, Timeout), "The handlers did not get every event.");
        }

        [Fact]
        public async Task TheEndOfAConnection_IsReported_AfterTheEventsOfThatConnection_AndOnce()
        {
            await using var server = new FakeObsWebSocketServer();
            var tap = new TapRecorder();
            using var client = await ConnectAsync(server, tap.Add);

            await server.SendEventAsync("SceneCreated", SceneData("Gaming"), eventIntent: 4);
            Assert.True(await tap.WaitForAsync(items => items.Any(item => item.Kind == IngressKind.Event)), "The event was not reported.");
            server.AbortConnection();

            Assert.True(await tap.WaitForAsync(items => items.Any(item => item.Kind == IngressKind.SessionEnded)), "The end of the connection was not reported.");
            await Task.Delay(100);
            var items = tap.Items;
            Assert.Equal(new[] { IngressKind.SessionStarted, IngressKind.Event, IngressKind.SessionEnded }, items.Select(item => item.Kind).ToArray());
            Assert.All(items, item => Assert.Equal(items[0].SessionId, item.SessionId));
            Assert.True(items[2].Sequence > items[1].Sequence);
        }

        [Fact]
        public async Task ANewConnection_IsAnotherSession_WithHigherNumbers()
        {
            await using var server = new FakeObsWebSocketServer();
            var tap = new TapRecorder();
            using var client = await ConnectAsync(server, tap.Add);

            client.Disconnect();
            Assert.True(await tap.WaitForAsync(items => items.Any(item => item.Kind == IngressKind.SessionEnded)), "The end of the connection was not reported.");
            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => client.ConnectionState == ConnectionState.Disconnected, Timeout), "The client did not disconnect.");

            var again = client.ConnectAsync(hostname: "127.0.0.1", port: server.Port);
            await server.HandshakeAsync().WithTimeoutAsync(Timeout);
            Assert.True(await again.WithTimeoutAsync(Timeout), "The client did not connect again.");

            var items = tap.Items;
            Assert.Equal(new[] { IngressKind.SessionStarted, IngressKind.SessionEnded, IngressKind.SessionStarted }, items.Select(item => item.Kind).ToArray());
            Assert.True(items[2].SessionId > items[0].SessionId);
            Assert.True(items[2].Sequence > items[1].Sequence);
            Assert.True(items[1].Sequence > items[0].Sequence);
        }

        [Fact]
        public async Task ATapThatThrows_DoesNotBreakTheConnection()
        {
            await using var server = new FakeObsWebSocketServer();
            using var client = await ConnectAsync(server, _ => throw new InvalidOperationException("A tap that fails."));

            var request = client.SendRawRequestAsync(RequestType.GetStudioModeEnabled, null);
            var (requestType, requestId, _) = await server.ReceiveRequestAsync(Timeout);
            await server.SendEventAsync("SceneCreated", SceneData("Gaming"), eventIntent: 4);
            await server.SendRequestSuccessAsync(requestType, requestId, new { studioModeEnabled = true });
            var response = await request.WithTimeoutAsync(Timeout);

            Assert.True(response.RequestStatus.Result);
            Assert.Equal(ConnectionState.Connected, client.ConnectionState);
        }

        [Fact]
        public async Task WithoutATap_TheMessagesAreStillNumbered()
        {
            await using var server = new FakeObsWebSocketServer();
            server.Start();
            using var client = new ObsClient { RequestTimeout = 5000 };
            var connect = client.ConnectAsync(hostname: "127.0.0.1", port: server.Port);
            await server.HandshakeAsync().WithTimeoutAsync(Timeout);
            await connect.WithTimeoutAsync(Timeout);

            var request = client.SendRawRequestAsync(RequestType.GetStudioModeEnabled, null);
            var (requestType, requestId, _) = await server.ReceiveRequestAsync(Timeout);
            await server.SendRequestSuccessAsync(requestType, requestId, new { studioModeEnabled = false });
            var response = await request.WithTimeoutAsync(Timeout);

            Assert.Equal(3, response.IngressSequence);
            Assert.Equal(3, client.LastIngress);
        }

        private static object SceneData(string name)
        {
            return new { sceneName = name, sceneUuid = Guid.NewGuid(), isGroup = false };
        }

        private static async Task<ObsClient> ConnectAsync(FakeObsWebSocketServer server, Action<IngressItem> tap)
        {
            server.Start();
            var client = new ObsClient { RequestTimeout = 5000 };
            client.SetIngressTap(tap);
            var connect = client.ConnectAsync(hostname: "127.0.0.1", port: server.Port);
            await server.HandshakeAsync().WithTimeoutAsync(Timeout);
            await connect.WithTimeoutAsync(Timeout);
            return client;
        }

        private sealed class TapRecorder
        {
            private readonly object _lock = new();
            private readonly List<IngressItem> _items = [];

            public IngressItem[] Items
            {
                get
                {
                    lock (_lock)
                    {
                        return _items.ToArray();
                    }
                }
            }

            public void Add(IngressItem item)
            {
                lock (_lock)
                {
                    _items.Add(item);
                }
            }

            public Task<bool> WaitForAsync(Func<IngressItem[], bool> condition)
            {
                return AsyncTestHelpers.WaitUntilAsync(() => condition(Items), Timeout);
            }
        }
    }
}
