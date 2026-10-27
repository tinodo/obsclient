namespace OBSStudioClient.Tests
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Tests.TestSupport;
    using Xunit;

    /// <summary>
    /// The events of OBS Studio, and ConnectionClosed, are not raised by the thread that reads from OBS Studio: it only queues them.
    /// A handler can wait for the response to a request, a slow handler does not hold up the responses, and the notifications
    /// come one at a time, in the order in which they happened.
    /// </summary>
    public class EventDispatchTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private static async Task<ObsClient> ConnectAsync(FakeObsWebSocketServer server)
        {
            server.Start();
            var client = new ObsClient { RequestTimeout = 5000 };
            var connect = client.ConnectAsync(hostname: "127.0.0.1", port: server.Port);
            await server.HandshakeAsync().WithTimeoutAsync(Timeout);
            await connect.WithTimeoutAsync(Timeout);
            return client;
        }

        private static Task SendSceneChangedAsync(FakeObsWebSocketServer server, string sceneName) =>
            server.SendEventAsync("CurrentProgramSceneChanged", new { sceneName, sceneUuid = Guid.NewGuid() });

        // What a handler of an application that does not use async does. It is a method of its own: blocking is the point of the tests.
        private static bool WaitForTheAnswer(Task<bool> request) => request.GetAwaiter().GetResult();

        private static void Block(ManualResetEventSlim gate) => gate.Wait(Timeout);

        [Fact]
        public async Task AHandlerThatBlocksOnARequest_GetsTheResponse()
        {
            // The thread of the handler waits for the response, and another thread reads it: the handler used to run on the thread that
            // reads, so the request could only end in a timeout.
            await using var server = new FakeObsWebSocketServer();
            using var client = await ConnectAsync(server);
            var outcome = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            client.CurrentProgramSceneChanged += (_, _) =>
            {
                try
                {
                    outcome.TrySetResult(WaitForTheAnswer(client.GetStudioModeEnabled()));
                }
                catch (Exception exception)
                {
                    outcome.TrySetException(exception);
                }
            };

            await SendSceneChangedAsync(server, "Scene");

            var (requestType, requestId, _) = await server.ReceiveRequestAsync(Timeout);
            Assert.Equal("GetStudioModeEnabled", requestType);
            await server.SendRequestSuccessAsync(requestType, requestId, new { studioModeEnabled = true });

            Assert.True(await outcome.Task.WithTimeoutAsync(Timeout, "The handler did not get the response to the request that it waited for."));
        }

        [Fact]
        public async Task ASlowHandler_DoesNotHoldUpTheResponseToARequest()
        {
            await using var server = new FakeObsWebSocketServer();
            using var client = await ConnectAsync(server);
            using var release = new ManualResetEventSlim(false);
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            client.CurrentProgramSceneChanged += (_, _) =>
            {
                entered.TrySetResult();
                Block(release);
            };

            try
            {
                await SendSceneChangedAsync(server, "Scene");
                await entered.Task.WithTimeoutAsync(Timeout, "The handler was not called.");

                // The handler still holds the event. A request is answered all the same: nothing waits behind the handler but the events.
                var request = client.GetStudioModeEnabled();
                var (requestType, requestId, _) = await server.ReceiveRequestAsync(Timeout);
                await server.SendRequestSuccessAsync(requestType, requestId, new { studioModeEnabled = true });

                Assert.True(await request.WithTimeoutAsync(Timeout, "The response was held up by a handler."));
            }
            finally
            {
                release.Set();
            }
        }

        [Fact]
        public async Task TheEvents_AreRaisedInTheOrderInWhichTheyArrived_AndNeverAtTheSameTime()
        {
            await using var server = new FakeObsWebSocketServer();
            using var client = await ConnectAsync(server);
            const int count = 20;
            var names = new List<string>();
            var running = 0;
            var overlaps = 0;
            client.CurrentProgramSceneChanged += (_, e) =>
            {
                if (Interlocked.Increment(ref running) > 1)
                {
                    Interlocked.Increment(ref overlaps);
                }

                // Slow enough for the events behind this one to queue up.
                Thread.Sleep(5);
                lock (names)
                {
                    names.Add(e.SceneName);
                }

                Interlocked.Decrement(ref running);
            };

            for (var index = 0; index < count; index++)
            {
                await SendSceneChangedAsync(server, $"Scene {index}");
            }

            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => { lock (names) { return names.Count == count; } }, Timeout), "Not every event was raised.");
            Assert.Equal(0, Volatile.Read(ref overlaps));
            lock (names)
            {
                Assert.Equal(Enumerable.Range(0, count).Select(index => $"Scene {index}"), names);
            }
        }

        [Fact]
        public async Task AHandlerThatThrows_DoesNotKeepTheEventsBehindItFromBeingRaised()
        {
            await using var server = new FakeObsWebSocketServer();
            using var client = await ConnectAsync(server);
            var seen = new List<string>();
            client.CurrentProgramSceneChanged += (_, e) =>
            {
                lock (seen)
                {
                    seen.Add(e.SceneName);
                }

                if (e.SceneName == "boom")
                {
                    throw new InvalidOperationException("handler failure");
                }
            };

            await SendSceneChangedAsync(server, "boom");
            await SendSceneChangedAsync(server, "after");

            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => { lock (seen) { return seen.Count == 2; } }, Timeout), "The event behind the handler that threw was not raised.");
            Assert.Equal(ConnectionState.Connected, client.ConnectionState);
        }

        [Fact]
        public async Task TheEventsThatArrivedBeforeTheConnectionEnded_AreRaisedBeforeConnectionClosed()
        {
            await using var server = new FakeObsWebSocketServer();
            using var client = await ConnectAsync(server);
            using var release = new ManualResetEventSlim(false);
            var order = new List<string>();
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            client.CurrentProgramSceneChanged += (_, e) =>
            {
                lock (order)
                {
                    order.Add(e.SceneName);
                }

                if (e.SceneName == "first")
                {
                    entered.TrySetResult();
                    Block(release);
                }
            };
            client.ConnectionClosed += (_, _) =>
            {
                lock (order)
                {
                    order.Add("closed");
                }

                closed.TrySetResult();
            };

            try
            {
                await SendSceneChangedAsync(server, "first");
                await entered.Task.WithTimeoutAsync(Timeout, "The first event was not raised.");

                // The first event holds the dispatcher. The second is queued behind it, and then the connection ends.
                await SendSceneChangedAsync(server, "second");
                await server.CloseWithCodeAsync(1001, "Going away").WithTimeoutAsync(Timeout);
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => client.ConnectionState == ConnectionState.Disconnected, Timeout));
            }
            finally
            {
                release.Set();
            }

            await closed.Task.WithTimeoutAsync(Timeout, "ConnectionClosed was not raised.");
            lock (order)
            {
                Assert.Equal(new[] { "first", "second", "closed" }, order);
            }
        }

        [Fact]
        public async Task AfterTheClientIsDisposed_NothingMoreIsRaised()
        {
            await using var server = new FakeObsWebSocketServer();
            var client = await ConnectAsync(server);
            using var release = new ManualResetEventSlim(false);
            var raised = new List<string>();
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            client.CurrentProgramSceneChanged += (_, e) =>
            {
                lock (raised)
                {
                    raised.Add(e.SceneName);
                }

                if (e.SceneName == "first")
                {
                    entered.TrySetResult();
                    Block(release);
                }
            };

            try
            {
                await SendSceneChangedAsync(server, "first");
                await entered.Task.WithTimeoutAsync(Timeout, "The first event was not raised.");
                await SendSceneChangedAsync(server, "second"); // queued behind the first, or refused because the client is disposed by then

                client.Dispose();
            }
            finally
            {
                release.Set();
            }

            // The queue is not worked off any more: the second event must not be raised, however long the handler of the first took.
            await Task.Delay(500);
            lock (raised)
            {
                Assert.Equal(new[] { "first" }, raised);
            }
        }

        [Fact]
        public async Task AConnectionClosedNotificationThatIsUnderWay_DoesNotReachTheNextHandler_WhenTheClientIsDisposed()
        {
            // The notification has left the queue, and a handler of it is running: emptying the queue cannot reach it, so it has to look at
            // the client again before the handler that is next.
            await using var server = new FakeObsWebSocketServer();
            using var client = await ConnectAsync(server);
            using var release = new ManualResetEventSlim(false);
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var next = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            client.ConnectionClosed += (_, _) =>
            {
                entered.TrySetResult();
                Block(release);
            };
            client.ConnectionClosed += (_, _) => next.TrySetResult();

            try
            {
                await server.CloseWithCodeAsync(1001, "Going away").WithTimeoutAsync(Timeout);
                await entered.Task.WithTimeoutAsync(Timeout, "ConnectionClosed was not raised.");

                client.Dispose();
            }
            finally
            {
                release.Set();
            }

            await Task.Delay(500);
            Assert.False(next.Task.IsCompleted, "A ConnectionClosed handler was called after the client was disposed.");
        }

        [Fact]
        public async Task AnEventThatIsUnderWay_DoesNotReachTheNextHandler_WhenTheClientIsDisposed()
        {
            await using var server = new FakeObsWebSocketServer();
            using var client = await ConnectAsync(server);
            using var release = new ManualResetEventSlim(false);
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var next = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            client.CurrentProgramSceneChanged += (_, _) =>
            {
                entered.TrySetResult();
                Block(release);
            };
            client.CurrentProgramSceneChanged += (_, _) => next.TrySetResult();

            try
            {
                await SendSceneChangedAsync(server, "Scene");
                await entered.Task.WithTimeoutAsync(Timeout, "The event was not raised.");

                client.Dispose();
            }
            finally
            {
                release.Set();
            }

            await Task.Delay(500);
            Assert.False(next.Task.IsCompleted, "An event handler was called after the client was disposed.");
        }

        [Fact]
        public async Task AConnectionClosedHandlerThatThrows_DoesNotKeepTheNextHandlerFromBeingCalled()
        {
            await using var server = new FakeObsWebSocketServer();
            using var client = await ConnectAsync(server);
            var next = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            client.ConnectionClosed += (_, _) => throw new InvalidOperationException("handler failure");
            client.ConnectionClosed += (_, _) => next.TrySetResult();

            await server.CloseWithCodeAsync(1001, "Going away").WithTimeoutAsync(Timeout);

            await next.Task.WithTimeoutAsync(Timeout, "The handler behind the one that threw was not called.");
        }
    }
}
