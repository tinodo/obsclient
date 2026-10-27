namespace OBSStudioClient.Tests
{
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

        private static async Task<ObsClient> ConnectAsync(FakeObsWebSocketServer server)
        {
            server.Start();
            var client = new ObsClient { RequestTimeout = 5000 };
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
            await using var server = new FakeObsWebSocketServer();
            using var client = await ConnectAsync(server);
            using var cancellation = new CancellationTokenSource();

            var request = client.GetStudioModeEnabled(cancellation.Token);
            var (requestType, requestId, _) = await server.ReceiveRequestAsync(Timeout);
            await server.SendRequestErrorAsync(requestType, requestId, (int)RequestStatusCode.NotReady, "Not ready");
            cancellation.Cancel();

            // Cancelling ends the call, whether the client is waiting for the answer or for the next attempt; it does not
            // give up with the error of OBS Studio, and it does not go through all the retries.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request.WithTimeoutAsync(Timeout));
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
