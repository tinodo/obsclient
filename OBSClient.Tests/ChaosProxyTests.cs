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
        public async Task AStall_CanBeEndedAtOnce_AndWhatWasHeldBackArrivesThen()
        {
            await using var server = new FakeObsWebSocketServer();
            server.Start();
            await using var proxy = new ChaosProxy("127.0.0.1", server.Port);
            proxy.Start();
            using var client = new ObsClient { RequestTimeout = 10000 };
            var connect = client.ConnectAsync(hostname: "127.0.0.1", port: proxy.Port);
            await server.HandshakeAsync().WithTimeoutAsync(Timeout);
            Assert.True(await connect.WithTimeoutAsync(Timeout));

            proxy.Stall(TimeSpan.FromMinutes(5));
            var call = client.GetSceneList();
            var received = server.ReceiveRequestAsync(Timeout);

            await Task.Delay(500);
            Assert.False(received.IsCompleted, "The request got through a stall of five minutes.");

            proxy.EndStall();
            var (requestType, requestId, _) = await received;
            await server.SendRequestSuccessAsync(requestType, requestId, new { scenes = Array.Empty<object>() });
            Assert.Empty((await call.WithTimeoutAsync(Timeout)).Scenes);
        }

        [Fact]
        public async Task TheTimerOfAStallThatWasEnded_DoesNotEndTheStallThatFollows()
        {
            await using var server = new FakeObsWebSocketServer();
            server.Start();
            await using var proxy = new ChaosProxy("127.0.0.1", server.Port);
            proxy.Start();
            using var client = new ObsClient { RequestTimeout = 10000 };
            var connect = client.ConnectAsync(hostname: "127.0.0.1", port: proxy.Port);
            await server.HandshakeAsync().WithTimeoutAsync(Timeout);
            Assert.True(await connect.WithTimeoutAsync(Timeout));

            // The timer of the first stall runs out after 400 ms, in the middle of the second one.
            proxy.Stall(TimeSpan.FromMilliseconds(400));
            proxy.EndStall();
            var watch = Stopwatch.StartNew();
            proxy.Stall(TimeSpan.FromMilliseconds(2000));
            var call = client.GetSceneList();

            var (requestType, requestId, _) = await server.ReceiveRequestAsync(Timeout);
            var arrivedAfter = watch.Elapsed;

            Assert.True(arrivedAfter >= TimeSpan.FromMilliseconds(1700), $"The request arrived after {arrivedAfter.TotalMilliseconds:N0} ms, in a stall of 2000 ms.");
            await server.SendRequestSuccessAsync(requestType, requestId, new { scenes = Array.Empty<object>() });
            Assert.Empty((await call.WithTimeoutAsync(Timeout)).Scenes);
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

            // How often the proxy is asked for one attempt depends on the runtime: the HTTP stack may try again after a reset (the macOS job
            // with .NET 10 saw 3 connections for one attempt), so the test counts what came in and does not expect one.
            var refusedAttempts = proxy.Accepted;
            Assert.True(refusedAttempts >= 1, "The proxy did not see the attempt that it refused.");
            Assert.Equal(0, proxy.Open);

            proxy.Refuse(false);
            var connect = client.ConnectAsync(hostname: "127.0.0.1", port: proxy.Port);
            await server.HandshakeAsync().WithTimeoutAsync(Timeout);
            Assert.True(await connect.WithTimeoutAsync(Timeout));
            Assert.Equal(refusedAttempts + 1, proxy.Accepted);
            Assert.Equal(1, proxy.Open);
        }
    }
}
