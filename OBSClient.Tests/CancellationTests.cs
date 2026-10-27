namespace OBSStudioClient.Tests
{
    using System.Net;
    using System.Net.Sockets;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Messages;
    using OBSStudioClient.Tests.TestSupport;
    using Xunit;

    /// <summary>
    /// Every request, <see cref="ObsClient.ConnectAsync"/>, <see cref="ObsClient.SendRequestBatchAsync"/> and
    /// <see cref="ObsClient.ReidentifyAsync"/> take a <see cref="CancellationToken"/>. Cancelling it ends the wait with an
    /// <see cref="OperationCanceledException"/> and leaves the connection usable; a request that runs out of time still
    /// throws a <see cref="TimeoutException"/>.
    /// </summary>
    public class CancellationTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private static async Task<ObsClient> ConnectAsync(FakeObsWebSocketServer server, ListLogger? log = null)
        {
            server.Start();
            var client = new ObsClient(log) { RequestTimeout = 5000 };
            var connect = client.ConnectAsync(hostname: "127.0.0.1", port: server.Port);
            await server.HandshakeAsync().WithTimeoutAsync(Timeout);
            await connect.WithTimeoutAsync(Timeout);
            return client;
        }

        // A round trip of a request that nothing else of the test has sent: it is the first message that the server receives.
        private static async Task AssertTheNextRequestIsTheFirstThatTheServerReceivesAsync(ObsClient client, FakeObsWebSocketServer server)
        {
            var request = client.GetCanvasList();
            var (requestType, requestId, _) = await server.ReceiveRequestAsync(Timeout);
            Assert.Equal("GetCanvasList", requestType);
            await server.SendRequestSuccessAsync(requestType, requestId, new { canvases = Array.Empty<object>() });
            Assert.Empty(await request.WithTimeoutAsync(Timeout));
        }

        [Fact]
        public async Task ARequestWithATokenThatIsNotCancelled_Completes()
        {
            await using var server = new FakeObsWebSocketServer();
            using var client = await ConnectAsync(server);
            using var cancellation = new CancellationTokenSource();

            var request = client.GetStudioModeEnabled(cancellation.Token);
            var (requestType, requestId, _) = await server.ReceiveRequestAsync(Timeout);
            await server.SendRequestSuccessAsync(requestType, requestId, new { studioModeEnabled = true });

            Assert.True(await request.WithTimeoutAsync(Timeout));
        }

        [Fact]
        public async Task ARequestWithACancelledToken_ThrowsAtOnce_AndSendsNothing()
        {
            await using var server = new FakeObsWebSocketServer();
            using var client = await ConnectAsync(server);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetStudioModeEnabled(cancellation.Token));

            // The request that was cancelled never went over the wire: the first request that the server gets is the next one.
            await AssertTheNextRequestIsTheFirstThatTheServerReceivesAsync(client, server);
            Assert.Equal(ConnectionState.Connected, client.ConnectionState);
        }

        [Fact]
        public async Task ARequestThatIsCancelledWhileItWaits_ThrowsOperationCanceled_AndTheConnectionStaysUsable()
        {
            await using var server = new FakeObsWebSocketServer();
            using var client = await ConnectAsync(server);
            using var cancellation = new CancellationTokenSource();

            var cancelled = client.GetStudioModeEnabled(cancellation.Token);
            var (requestType, requestId, _) = await server.ReceiveRequestAsync(Timeout); // it is sent, and not answered yet
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled.WithTimeoutAsync(Timeout));
            Assert.Equal(ConnectionState.Connected, client.ConnectionState);

            // OBS Studio answers after all: nothing waits for that response any more, and it must not hurt the connection.
            await server.SendRequestSuccessAsync(requestType, requestId, new { studioModeEnabled = true });

            await AssertTheNextRequestIsTheFirstThatTheServerReceivesAsync(client, server);
        }

        [Fact]
        public async Task ARequestThatRunsOutOfTime_WithATokenThatIsNotCancelled_StillThrowsTimeoutException()
        {
            await using var server = new FakeObsWebSocketServer();
            using var client = await ConnectAsync(server);
            client.RequestTimeout = 300;
            using var cancellation = new CancellationTokenSource();

            var request = client.GetStudioModeEnabled(cancellation.Token);
            await server.ReceiveRequestAsync(Timeout); // consumed, and deliberately not answered

            // The safety timeout is applied to the wait, not to the assertion: only the client's own exception may satisfy it.
            var finished = await Task.WhenAny(request, Task.Delay(Timeout));
            Assert.Same(request, finished);
            await Assert.ThrowsAsync<TimeoutException>(() => request);
        }

        [Fact]
        public async Task TheWaitBetweenTheRetriesOfARequestThatIsNotReady_CanBeCancelled()
        {
            // OBS Studio answers NotReady (207) when it has just started; the client asks again after RequestRetryInterval, up to ten times.
            var log = new ListLogger();
            await using var server = new FakeObsWebSocketServer();
            using var client = await ConnectAsync(server, log);
            client.RequestRetryInterval = 60_000; // far longer than this test waits: a wait that ignores the token cannot end in time
            using var cancellation = new CancellationTokenSource();

            var request = client.GetStudioModeEnabled(cancellation.Token);
            var (requestType, requestId, _) = await server.ReceiveRequestAsync(Timeout);
            await server.SendRequestErrorAsync(requestType, requestId, (int)RequestStatusCode.NotReady, "Not ready");

            // The client logs the answer when it reads it, and then needs a moment to get to the wait for its next attempt.
            Assert.True(
                await AsyncTestHelpers.WaitUntilAsync(() => log.Entries.Any(entry => entry.Message.Contains("\"code\":207", StringComparison.Ordinal)), Timeout),
                "The client did not read the answer of OBS Studio.");
            await Task.Delay(300);
            cancellation.Cancel();

            // Cancelling ends the call at once. It does not end with the error of OBS Studio, and a wait that ignored the token would take a minute.
            var finished = await Task.WhenAny(request, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.Same(request, finished);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
        }

        [Fact]
        public async Task ABatchThatIsCancelledWhileItWaits_ThrowsOperationCanceled_AndTheConnectionStaysUsable()
        {
            await using var server = new FakeObsWebSocketServer();
            using var client = await ConnectAsync(server);
            using var cancellation = new CancellationTokenSource();

            var batch = new RequestBatchMessage();
            batch.AddGetStudioModeEnabledRequest();
            var sending = client.SendRequestBatchAsync(batch, cancellationToken: cancellation.Token);
            using var received = await server.ReceiveJsonAsync(Timeout); // the batch is sent, and not answered
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sending.WithTimeoutAsync(Timeout));
            Assert.Equal(ConnectionState.Connected, client.ConnectionState);

            await AssertTheNextRequestIsTheFirstThatTheServerReceivesAsync(client, server);
        }

        [Fact]
        public async Task ReidentifyAsync_WithACancelledToken_ThrowsAndSendsNothing()
        {
            await using var server = new FakeObsWebSocketServer();
            using var client = await ConnectAsync(server);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.ReidentifyAsync(EventSubscriptions.None, cancellation.Token));

            // No Reidentify message was sent: the first message that the server gets is the request.
            await AssertTheNextRequestIsTheFirstThatTheServerReceivesAsync(client, server);
        }

        [Fact]
        public async Task ConnectAsync_WithACancelledToken_ThrowsWithoutConnecting()
        {
            await using var server = new FakeObsWebSocketServer();
            server.Start();
            using var client = new ObsClient();
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.ConnectAsync(hostname: "127.0.0.1", port: server.Port, cancellationToken: cancellation.Token));

            await Assert.ThrowsAsync<TimeoutException>(() => server.WaitForConnectionAsync(TimeSpan.FromMilliseconds(500)));
            Assert.Equal(ConnectionState.Disconnected, client.ConnectionState);
        }

        [Fact]
        public async Task ConnectAsync_CancelledWhileItWaitsForTheHandshake_ThrowsAndLeavesNothingConnected()
        {
            await using var server = new FakeObsWebSocketServer();
            server.Start();
            using var client = new ObsClient();
            using var cancellation = new CancellationTokenSource();

            var connect = client.ConnectAsync(autoReconnect: true, hostname: "127.0.0.1", port: server.Port, cancellationToken: cancellation.Token);
            await server.WaitForConnectionAsync(Timeout); // the connection is open, and the server says nothing: the client waits for its Hello
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => connect.WithTimeoutAsync(Timeout));
            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => client.ConnectionState == ConnectionState.Disconnected, Timeout));

            // Cancelling ends the attempt the way Disconnect() does, so the client does not go on trying behind the caller's back.
            Assert.False(client.AutoReconnect);
        }

        [Fact]
        public async Task ACancelledConnectAsync_DoesNotDisturbAConnectionThatAHandlerStartedInItsPlace()
        {
            // The end of the call that was cancelled and the handler that connects again run at the same time on different threads, and a call
            // that reaches the new connection hits it only when they overlap by a few microseconds: the scenario is repeated, to make that
            // overlap likely, as a failure of one round says nothing about the others.
            for (var round = 0; round < 30; round++)
            {
                await ACancelledConnectAsyncAndAHandlerThatConnectsAgainAsync(round);
            }
        }

        private static async Task ACancelledConnectAsyncAndAHandlerThatConnectsAgainAsync(int round)
        {
            // A handler of ConnectionState that connects again as soon as the connection is gone starts a new connection, while the
            // call that was cancelled is still on its way out: that call must end its own attempt, and nothing of the new one.
            await using var server = new FakeObsWebSocketServer();
            server.Start();
            using var client = new ObsClient();
            using var cancellation = new CancellationTokenSource();
            Task<bool>? replacement = null;
            client.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ObsClient.ConnectionState) && client.ConnectionState == ConnectionState.Disconnected && replacement is null)
                {
                    replacement = client.ConnectAsync(autoReconnect: true, hostname: "127.0.0.1", port: server.Port);
                }
            };

            var first = client.ConnectAsync(hostname: "127.0.0.1", port: server.Port, cancellationToken: cancellation.Token);
            await server.WaitForConnectionAsync(Timeout); // open, and the server says nothing: the client waits for its Hello
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first.WithTimeoutAsync(Timeout));

            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => replacement is not null, Timeout), $"The handler did not connect again (round {round}).");
            await server.HandshakeAsync(timeout: Timeout).WithTimeoutAsync(Timeout, "The connection that the handler started did not get its handshake.");
            Assert.True(await replacement!.WithTimeoutAsync(Timeout));
            Assert.Equal(ConnectionState.Connected, client.ConnectionState);

            // The setting of the new connection is its own: the call that was cancelled did not switch it off.
            Assert.True(client.AutoReconnect, $"The call that was cancelled switched AutoReconnect off for the connection that replaced it (round {round}).");

            client.Disconnect();
        }

        [Fact]
        public async Task ATokenThatTheHandlerOfAFailedAttemptCancels_StillSwitchesAutoReconnectOff()
        {
            // Nothing listens on the port, so the attempt fails on its own. The handler that is told about it cancels the token before
            // ConnectAsync has ended: the call throws, and the client must not go on trying behind the caller's back.
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();

            using var client = new ObsClient();
            using var cancellation = new CancellationTokenSource();
            client.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ObsClient.ConnectionState) && client.ConnectionState == ConnectionState.Disconnected)
                {
                    cancellation.Cancel();
                }
            };

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.ConnectAsync(autoReconnect: true, hostname: "127.0.0.1", port: port, cancellationToken: cancellation.Token).WithTimeoutAsync(Timeout));

            Assert.False(client.AutoReconnect);
        }

        [Fact]
        public async Task ATokenThatIsCancelledAsTheConnectionIsEstablished_EitherEndsTheAttemptOrChangesNothing()
        {
            // Whichever comes first decides, and the call never does both: it throws and the connection is gone, or it completes and the
            // token no longer matters. The handler cancels at the first moment that the connection is established.
            for (var round = 0; round < 10; round++)
            {
                await using var server = new FakeObsWebSocketServer();
                server.Start();
                using var client = new ObsClient();
                using var cancellation = new CancellationTokenSource();
                client.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(ObsClient.ConnectionState) && client.ConnectionState == ConnectionState.Connected)
                    {
                        cancellation.Cancel();
                    }
                };

                var connect = client.ConnectAsync(autoReconnect: true, hostname: "127.0.0.1", port: server.Port, cancellationToken: cancellation.Token);
                await server.HandshakeAsync(timeout: Timeout).WithTimeoutAsync(Timeout);

                bool completed;
                try
                {
                    completed = await connect.WithTimeoutAsync(Timeout);
                }
                catch (OperationCanceledException)
                {
                    completed = false;
                }

                if (completed)
                {
                    await Task.Delay(100);
                    Assert.True(client.ConnectionState == ConnectionState.Connected, $"The call completed, and the token ended the connection all the same (round {round}).");
                }
                else
                {
                    Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => client.ConnectionState == ConnectionState.Disconnected, Timeout), $"The call threw, and the connection is still there (round {round}).");
                    Assert.False(client.AutoReconnect, $"The call threw, and AutoReconnect is still on (round {round}).");
                }

                client.Disconnect();
            }
        }

        [Fact]
        public async Task ReidentifyAsync_ThatIsCancelled_DoesNotChangeWhatAReconnectSubscribesTo()
        {
            await using var server = new FakeObsWebSocketServer();
            server.Start();
            using var client = new ObsClient { RequestTimeout = 5000 };
            var connect = client.ConnectAsync(autoReconnect: true, hostname: "127.0.0.1", port: server.Port, eventSubscription: EventSubscriptions.Scenes);
            await server.HandshakeAsync().WithTimeoutAsync(Timeout);
            await connect.WithTimeoutAsync(Timeout);

            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.ReidentifyAsync(EventSubscriptions.None, cancellation.Token));

            // The reconnect sends what the client was asked to subscribe to when it connected: a call that did not happen changed nothing.
            server.AbortConnection();
            using var identify = await server.HandshakeAsync(timeout: Timeout).WithTimeoutAsync(Timeout, "The client did not reconnect.");
            Assert.Equal((int)EventSubscriptions.Scenes, identify.RootElement.GetProperty("d").GetProperty("eventSubscriptions").GetInt32());

            client.Disconnect();
        }

        [Fact]
        public async Task ReidentifyAsync_ThatWasSent_ChangesWhatAReconnectSubscribesTo()
        {
            await using var server = new FakeObsWebSocketServer();
            server.Start();
            using var client = new ObsClient { RequestTimeout = 5000 };
            var connect = client.ConnectAsync(autoReconnect: true, hostname: "127.0.0.1", port: server.Port, eventSubscription: EventSubscriptions.Scenes);
            await server.HandshakeAsync().WithTimeoutAsync(Timeout);
            await connect.WithTimeoutAsync(Timeout);

            await client.ReidentifyAsync(EventSubscriptions.Inputs).WithTimeoutAsync(Timeout);
            using var reidentify = await server.ReceiveJsonAsync(Timeout);
            Assert.Equal((int)EventSubscriptions.Inputs, reidentify.RootElement.GetProperty("d").GetProperty("eventSubscriptions").GetInt32());

            server.AbortConnection();
            using var identify = await server.HandshakeAsync(timeout: Timeout).WithTimeoutAsync(Timeout, "The client did not reconnect.");
            Assert.Equal((int)EventSubscriptions.Inputs, identify.RootElement.GetProperty("d").GetProperty("eventSubscriptions").GetInt32());

            client.Disconnect();
        }

        [Fact]
        public async Task ATokenThatIsCancelledAfterConnectAsyncReturned_DoesNotCloseTheConnection()
        {
            await using var server = new FakeObsWebSocketServer();
            server.Start();
            using var client = new ObsClient();
            using var cancellation = new CancellationTokenSource();

            var connect = client.ConnectAsync(hostname: "127.0.0.1", port: server.Port, cancellationToken: cancellation.Token);
            await server.HandshakeAsync().WithTimeoutAsync(Timeout);
            Assert.True(await connect.WithTimeoutAsync(Timeout));

            cancellation.Cancel();

            await Task.Delay(300);
            Assert.Equal(ConnectionState.Connected, client.ConnectionState);
            await AssertTheNextRequestIsTheFirstThatTheServerReceivesAsync(client, server);
        }
    }
}
