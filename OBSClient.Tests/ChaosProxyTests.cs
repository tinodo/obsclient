namespace OBSStudioClient.Tests
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Tests.TestSupport;
    using System.Diagnostics;
    using Xunit;

    /// <summary>
    /// The proxy that the soak test puts between the client and a real OBS Studio (<see cref="ChaosProxy"/>), tried with the fake server: a
    /// proxy that does not do what it says would make the soak test prove nothing. These run on every build; the soak test itself only
    /// runs against a real OBS Studio, for minutes.
    /// </summary>
    public class ChaosProxyTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

        private static async Task AnswerAsync(FakeObsWebSocketServer server)
        {
            var (requestType, requestId, _) = await server.ReceiveRequestAsync(Timeout);
            await server.SendRequestSuccessAsync(requestType, requestId, new { scenes = Array.Empty<object>() });
        }

        [Fact]
        public async Task WhatIsSent_ReachesTheServerThroughTheProxy_AndTheAnswerComesBack()
        {
            await using var server = new FakeObsWebSocketServer();
            server.Start();
            await using var proxy = new ChaosProxy("127.0.0.1", server.Port);
            proxy.Start();
            using var client = new ObsClient { RequestTimeout = 5000 };

            var connect = client.ConnectAsync(hostname: "127.0.0.1", port: proxy.Port);
            await server.HandshakeAsync().WithTimeoutAsync(Timeout);
            Assert.True(await connect.WithTimeoutAsync(Timeout));

            var call = client.GetSceneList();
            await AnswerAsync(server);
            var response = await call.WithTimeoutAsync(Timeout);

            Assert.Empty(response.Scenes);
            Assert.Equal(1, proxy.Accepted);
            Assert.Equal(1, proxy.Open);
        }

        [Fact]
        public async Task ACut_ResetsTheConnection_AndAutoReconnectComesBackThroughTheProxy()
        {
            await using var server = new FakeObsWebSocketServer();
            server.Start();
            await using var proxy = new ChaosProxy("127.0.0.1", server.Port);
            proxy.Start();
            using var client = new ObsClient { RequestTimeout = 5000 };
            var closed = new EventAwaiter<ConnectionClosedEventArgs>();
            client.ConnectionClosed += closed.Handler;
            var connect = client.ConnectAsync(autoReconnect: true, hostname: "127.0.0.1", port: proxy.Port);
            await server.HandshakeAsync().WithTimeoutAsync(Timeout);
            Assert.True(await connect.WithTimeoutAsync(Timeout));

            Assert.Equal(1, proxy.Cut());

            await closed.Task.WithTimeoutAsync(Timeout, "ConnectionClosed was not raised after the connection was cut.");
            Assert.Equal(1, proxy.ConnectionsCut);

            // The reconnect timer ticks every 3 seconds: the next connection comes through the proxy as well.
            await server.HandshakeAsync().WithTimeoutAsync(Timeout, "The client did not connect again.");
            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => client.ConnectionState == ConnectionState.Connected, Timeout));
            Assert.Equal(2, proxy.Accepted);

            var call = client.GetSceneList();
            await AnswerAsync(server);
            await call.WithTimeoutAsync(Timeout, "The connection that was made again does not carry requests.");
        }

        [Fact]
        public async Task AStall_HoldsBackWhatIsSent_UntilItIsOver_AndThenAllOfItArrives()
        {
            await using var server = new FakeObsWebSocketServer();
            server.Start();
            await using var proxy = new ChaosProxy("127.0.0.1", server.Port);
            proxy.Start();
            using var client = new ObsClient { RequestTimeout = 10000 };
            var connect = client.ConnectAsync(hostname: "127.0.0.1", port: proxy.Port);
            await server.HandshakeAsync().WithTimeoutAsync(Timeout);
            Assert.True(await connect.WithTimeoutAsync(Timeout));

            var watch = Stopwatch.StartNew();
            proxy.Stall(TimeSpan.FromMilliseconds(1500));
            var call = client.GetSceneList();

            var (requestType, requestId, _) = await server.ReceiveRequestAsync(Timeout);
            var arrivedAfter = watch.Elapsed;

            // The request reached the server when the stall was over, not before.
            Assert.True(arrivedAfter >= TimeSpan.FromMilliseconds(1300), $"The request arrived after {arrivedAfter.TotalMilliseconds:N0} ms, in a stall of 1500 ms.");

            // Nothing was lost: the answer comes back through the proxy, and the call gets it.
            await server.SendRequestSuccessAsync(requestType, requestId, new { scenes = Array.Empty<object>() });
            var response = await call.WithTimeoutAsync(Timeout);
            Assert.Empty(response.Scenes);
        }

        [Fact]
        public async Task ARefusedConnection_IsResetAtOnce_UntilTheProxyPassesConnectionsOnAgain()
        {
            await using var server = new FakeObsWebSocketServer();
            server.Start();
            await using var proxy = new ChaosProxy("127.0.0.1", server.Port);
            proxy.Start();
            using var client = new ObsClient { RequestTimeout = 5000 };

            proxy.Refuse(true);
            var refused = await client.ConnectAsync(hostname: "127.0.0.1", port: proxy.Port).WithTimeoutAsync(Timeout);

            Assert.False(refused);
            Assert.Equal(ConnectionState.Disconnected, client.ConnectionState);
            Assert.Equal(1, proxy.Accepted);
            Assert.Equal(0, proxy.Open);

            proxy.Refuse(false);
            var connect = client.ConnectAsync(hostname: "127.0.0.1", port: proxy.Port);
            await server.HandshakeAsync().WithTimeoutAsync(Timeout);
            Assert.True(await connect.WithTimeoutAsync(Timeout));
            Assert.Equal(2, proxy.Accepted);
        }
    }
}
