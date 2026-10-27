namespace OBSStudioClient.Tests
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Tests.TestSupport;
    using Xunit;

    /// <summary>
    /// Exercises the connection lifecycle against a fake obs-websocket server: handshake/authentication,
    /// deliberate disconnects, and the abrupt-disconnect handling fixed for
    /// https://github.com/tinodo/obsclient/issues/15 and https://github.com/tinodo/obsclient/issues/17.
    /// </summary>
    public class ConnectionLifecycleTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        [Fact]
        public async Task ConnectAsync_NoAuthRequired_TransitionsToConnected()
        {
            await using var server = new FakeObsWebSocketServer();
            server.Start();
            using var client = new ObsClient();

            var connectTask = client.ConnectAsync(hostname: "127.0.0.1", port: server.Port);
            await server.HandshakeAsync().WithTimeoutAsync(Timeout);
            var connected = await connectTask.WithTimeoutAsync(Timeout);

            Assert.True(connected);
            Assert.Equal(ConnectionState.Connected, client.ConnectionState);

            client.Disconnect();
        }

        [Fact]
        public async Task ConnectAsync_WithCorrectPassword_AuthenticatesSuccessfully()
        {
            await using var server = new FakeObsWebSocketServer();
            server.Start();
            using var client = new ObsClient();

            var connectTask = client.ConnectAsync(password: "P@ssw0rd!", hostname: "127.0.0.1", port: server.Port);
            await server.HandshakeAsync(password: "P@ssw0rd!").WithTimeoutAsync(Timeout);
            var connected = await connectTask.WithTimeoutAsync(Timeout);

            Assert.True(connected);
            Assert.Equal(ConnectionState.Connected, client.ConnectionState);

            client.Disconnect();
        }

        [Theory]
        [InlineData("P@ssw0rd!", "Ub42d25FQ+K1NOWmd9kA1fT417w7cg95vrg13T009Mk=")]
        [InlineData("p\u00e4ssword", "A9jzi81MGl6JsHHXds/5dYT+sPOw3GvoIFOu4Lxvug0=")]
        [InlineData("\u043f\u0430\u0440\u043e\u043b\u044c", "oFSBMYAi9U6b/iNucuzBlCtaCLx0fPCW2xOPqEb7F9E=")]
        [InlineData("\u5bc6\u7801\U0001F512", "/5EcgIKE7Pu0ldOuEPfZWI1x9Y+q2eIaD0wO3QSLbVc=")]
        public async Task ConnectAsync_AuthenticationString_MatchesTheUtf8ReferenceVector(string password, string expectedAuthentication)
        {
            // obs-websocket hashes the UTF-8 bytes of the password (Utils::Crypto::GenerateSecret/CheckAuthenticationString).
            // The vectors were computed independently (Python hashlib) for the fake server's fixed salt and challenge,
            // so a client and fake server that agree on the wrong encoding cannot both pass.
            await using var server = new FakeObsWebSocketServer();
            server.Start();
            using var client = new ObsClient();

            var connectTask = client.ConnectAsync(password: password, hostname: "127.0.0.1", port: server.Port);
            using var identify = await server.HandshakeAsync(password: password).WithTimeoutAsync(Timeout);
            var connected = await connectTask.WithTimeoutAsync(Timeout);

            Assert.True(connected);
            Assert.Equal(expectedAuthentication, identify.RootElement.GetProperty("d").GetProperty("authentication").GetString());

            client.Disconnect();
        }

        [Fact]
        public async Task Disconnect_AfterDeliberateCall_FiresConnectionClosedWithNormalClosure()
        {
            // Regression test: Task.Run used to be given the (already-cancelled) receiver token, which meant
            // ConnectionClosed never actually fired on a deliberate Disconnect().
            await using var server = new FakeObsWebSocketServer();
            server.Start();
            using var client = new ObsClient();
            var closedAwaiter = new EventAwaiter<ConnectionClosedEventArgs>();
            client.ConnectionClosed += closedAwaiter.Handler;

            var connectTask = client.ConnectAsync(hostname: "127.0.0.1", port: server.Port);
            await server.HandshakeAsync().WithTimeoutAsync(Timeout);
            await connectTask.WithTimeoutAsync(Timeout);

            client.Disconnect();

            var args = await closedAwaiter.Task.WithTimeoutAsync(Timeout, "ConnectionClosed did not fire after a deliberate Disconnect().");

            Assert.Equal(WebSocketCloseCode.NormalClosure, args.WebSocketCloseCode);
            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => client.ConnectionState == ConnectionState.Disconnected, Timeout));
        }

        [Fact]
        public async Task AbruptDisconnect_TransitionsToDisconnectedAndFiresConnectionClosed()
        {
            // Regression test for https://github.com/tinodo/obsclient/issues/17: a connection that disappears
            // without a WebSocket close handshake must still make ObsClient observe the disconnect.
            await using var server = new FakeObsWebSocketServer();
            server.Start();
            using var client = new ObsClient();
            var closedAwaiter = new EventAwaiter<ConnectionClosedEventArgs>();
            client.ConnectionClosed += closedAwaiter.Handler;

            var connectTask = client.ConnectAsync(hostname: "127.0.0.1", port: server.Port);
            await server.HandshakeAsync().WithTimeoutAsync(Timeout);
            await connectTask.WithTimeoutAsync(Timeout);
            Assert.Equal(ConnectionState.Connected, client.ConnectionState);

            server.AbortConnection();

            await closedAwaiter.Task.WithTimeoutAsync(Timeout, "ConnectionClosed did not fire after an abrupt disconnect.");
            Assert.True(
                await AsyncTestHelpers.WaitUntilAsync(() => client.ConnectionState == ConnectionState.Disconnected, Timeout),
                $"ConnectionState got stuck at {client.ConnectionState} instead of reaching Disconnected after an abrupt disconnect.");
        }

        [Fact]
        public async Task AbruptDisconnect_WithAutoReconnectEnabled_ReconnectsAutomatically()
        {
            // Regression test for https://github.com/tinodo/obsclient/issues/17: AutoReconnect only re-engages
            // once ConnectionState correctly reaches Disconnected.
            await using var server = new FakeObsWebSocketServer();
            server.Start();
            using var client = new ObsClient();

            var connectTask = client.ConnectAsync(autoReconnect: true, hostname: "127.0.0.1", port: server.Port);
            await server.HandshakeAsync().WithTimeoutAsync(Timeout);
            await connectTask.WithTimeoutAsync(Timeout);
            Assert.Equal(ConnectionState.Connected, client.ConnectionState);

            server.AbortConnection();

            Assert.True(
                await AsyncTestHelpers.WaitUntilAsync(() => client.ConnectionState == ConnectionState.Disconnected, Timeout),
                "Client never observed the abrupt disconnect.");

            // The reconnect timer polls every few seconds; give it room, then service the new connection attempt.
            await server.HandshakeAsync(timeout: Timeout).WithTimeoutAsync(Timeout, "Client did not attempt to reconnect in time.");

            Assert.True(
                await AsyncTestHelpers.WaitUntilAsync(() => client.ConnectionState == ConnectionState.Connected, Timeout),
                "Client did not reach Connected again after AutoReconnect kicked in.");

            client.Disconnect();
        }

        [Fact]
        public async Task SessionInvalidated_WithAutoReconnectEnabled_DoesNotReconnect()
        {
            // obs-websocket documents that a client must not reconnect automatically after close code 4011 (the Kick
            // button of the session list of OBS Studio), so AutoReconnect is switched off and the client stays disconnected.
            await using var server = new FakeObsWebSocketServer();
            server.Start();
            using var client = new ObsClient();
            var closedAwaiter = new EventAwaiter<ConnectionClosedEventArgs>();
            client.ConnectionClosed += closedAwaiter.Handler;

            var connectTask = client.ConnectAsync(autoReconnect: true, hostname: "127.0.0.1", port: server.Port);
            await server.HandshakeAsync().WithTimeoutAsync(Timeout);
            await connectTask.WithTimeoutAsync(Timeout);
            Assert.True(client.AutoReconnect);

            await server.CloseWithCodeAsync(4011, "SessionInvalidated").WithTimeoutAsync(Timeout);

            var args = await closedAwaiter.Task.WithTimeoutAsync(Timeout, "ConnectionClosed did not fire after the session was invalidated.");
            Assert.Equal(WebSocketCloseCode.SessionInvalidated, args.WebSocketCloseCode);
            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => client.ConnectionState == ConnectionState.Disconnected, Timeout));
            Assert.False(client.AutoReconnect);

            // The reconnect timer ticks every 3 seconds: waiting longer than that shows that no attempt is made.
            await Assert.ThrowsAsync<TimeoutException>(() => server.WaitForConnectionAsync(TimeSpan.FromSeconds(4)));
            Assert.Equal(ConnectionState.Disconnected, client.ConnectionState);
        }

        [Fact]
        public async Task ClosedByObsStudioWithAnotherCode_WithAutoReconnectEnabled_StillReconnects()
        {
            // Only the Kick button ends the reconnecting: an OBS Studio that goes away (1001) can come back, and the client follows.
            await using var server = new FakeObsWebSocketServer();
            server.Start();
            using var client = new ObsClient();

            var connectTask = client.ConnectAsync(autoReconnect: true, hostname: "127.0.0.1", port: server.Port);
            await server.HandshakeAsync().WithTimeoutAsync(Timeout);
            await connectTask.WithTimeoutAsync(Timeout);

            await server.CloseWithCodeAsync(1001, "Going away").WithTimeoutAsync(Timeout);

            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => client.ConnectionState == ConnectionState.Disconnected, Timeout));
            Assert.True(client.AutoReconnect);

            await server.HandshakeAsync(timeout: Timeout).WithTimeoutAsync(Timeout, "Client did not attempt to reconnect in time.");
            Assert.True(
                await AsyncTestHelpers.WaitUntilAsync(() => client.ConnectionState == ConnectionState.Connected, Timeout),
                "Client did not reach Connected again after AutoReconnect kicked in.");

            client.Disconnect();
        }

        [Fact]
        public async Task ConnectionLostDuringTheHandshake_ConnectAsyncReturnsFalse_InsteadOfHanging()
        {
            // Nothing completed the authentication task when the connection ended before OBS Studio said
            // Identified (other than a rejected password), so ConnectAsync could wait forever.
            await using var server = new FakeObsWebSocketServer();
            server.Start();
            using var client = new ObsClient();

            var connectTask = client.ConnectAsync(hostname: "127.0.0.1", port: server.Port);
            await server.WaitForConnectionAsync(Timeout);
            await server.SendRawAsync("{\"op\":0,\"d\":{\"obsStudioVersion\":\"30.2.2\",\"obsWebSocketVersion\":\"5.7.6\",\"rpcVersion\":1}}");
            await server.ReceiveJsonAsync(Timeout); // the client's Identify message
            server.AbortConnection();

            var connected = await connectTask.WithTimeoutAsync(Timeout, "ConnectAsync never completed after the connection was lost during the handshake.");

            Assert.False(connected);
            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => client.ConnectionState == ConnectionState.Disconnected, Timeout));
        }

        [Fact]
        public async Task ReconnectFromAConnectionStateHandler_DoesNotDisturbTheNewConnection()
        {
            // A handler that reconnects as soon as ConnectionState becomes Disconnected runs on the receive loop of
            // the connection that just ended; that loop must not tear down the connection the handler just started.
            await using var server = new FakeObsWebSocketServer();
            server.Start();
            using var client = new ObsClient { RequestTimeout = 5000 };

            Task<bool>? secondConnect = null;
            client.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ObsClient.ConnectionState) && client.ConnectionState == ConnectionState.Disconnected && secondConnect is null)
                {
                    secondConnect = client.ConnectAsync(hostname: "127.0.0.1", port: server.Port);
                }
            };

            var firstConnect = client.ConnectAsync(hostname: "127.0.0.1", port: server.Port);
            await server.HandshakeAsync().WithTimeoutAsync(Timeout);
            await firstConnect.WithTimeoutAsync(Timeout);

            server.AbortConnection();
            await server.HandshakeAsync(timeout: Timeout).WithTimeoutAsync(Timeout, "The client did not reconnect.");

            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => secondConnect is not null, Timeout));
            Assert.True(await secondConnect!.WithTimeoutAsync(Timeout));
            Assert.Equal(ConnectionState.Connected, client.ConnectionState);

            // The new connection is usable, so nothing of it was disposed by the old one.
            var request = client.GetStudioModeEnabled();
            var (requestType, requestId, _) = await server.ReceiveRequestAsync(Timeout);
            await server.SendRequestSuccessAsync(requestType, requestId, new { studioModeEnabled = true });
            Assert.True(await request.WithTimeoutAsync(Timeout));

            client.Disconnect();
        }

        [Fact]
        public async Task AutoReconnect_PropertyChangedHandlerThatWaitsForAnotherThread_DoesNotDeadlock()
        {
            // A handler that hands work to another thread, which reads the client (as a UI handler does), deadlocked
            // while the notification was raised from inside the client's lock.
            using var client = new ObsClient();
            client.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ObsClient.AutoReconnect))
                {
                    Task.Run(() => client.AutoReconnect).Wait();
                }
            };

            await Task.Run(() => client.AutoReconnect = false).WithTimeoutAsync(Timeout, "Setting AutoReconnect deadlocked in its PropertyChanged handler.");
        }

        [Fact]
        public void AutoReconnect_PropertyChangedHandlerThatThrows_DoesNotBreakTheSetterOrDisconnect()
        {
            // Disconnect() and ConnectAsync() set AutoReconnect, so a failing handler must not be able to break them.
            using var client = new ObsClient();
            client.PropertyChanged += (_, _) => throw new InvalidOperationException("handler failure");

            var setterFailure = Record.Exception(() => client.AutoReconnect = false);
            var disconnectFailure = Record.Exception(() => client.Disconnect());

            Assert.Null(setterFailure);
            Assert.Null(disconnectFailure);
            Assert.False(client.AutoReconnect);
        }
    }
}
