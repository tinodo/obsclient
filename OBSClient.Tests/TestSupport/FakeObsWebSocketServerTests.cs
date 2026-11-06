namespace OBSStudioClient.Tests.TestSupport
{
    using System.Net;
    using System.Net.Sockets;

    /// <summary>
    /// The fake OBS Studio of the tests. A test that ends a connection at any moment (a call that is cancelled while it connects) must not keep it from accepting
    /// the next one.
    /// </summary>
    public class FakeObsWebSocketServerTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        [Fact]
        public async Task AClientThatNeverSendsItsRequest_DoesNotKeepTheNextOneFromConnecting()
        {
            await using var server = new FakeObsWebSocketServer();
            server.Start();

            // A connection that is open and silent: a call that is cancelled while it connects leaves one (the connection pool of the client keeps it open).
            using var silent = new TcpClient();
            await silent.ConnectAsync(IPAddress.Loopback, server.Port).WaitAsync(Timeout);

            using var client = new ObsClient();
            var connect = client.ConnectAsync(hostname: "127.0.0.1", port: server.Port);
            await server.HandshakeAsync().WithTimeoutAsync(Timeout);

            Assert.True(await connect.WithTimeoutAsync(Timeout));
        }

        [Fact]
        public async Task AClientThatGoesAwayBeforeTheUpgrade_DoesNotKeepTheNextOneFromConnecting()
        {
            await using var server = new FakeObsWebSocketServer();
            server.Start();

            // A connection that is closed before it has sent a word: the server cannot complete an upgrade for it.
            using (var gone = new TcpClient())
            {
                await gone.ConnectAsync(IPAddress.Loopback, server.Port).WaitAsync(Timeout);
            }

            // One that is closed in the middle of the request.
            using (var cut = new TcpClient())
            {
                await cut.ConnectAsync(IPAddress.Loopback, server.Port).WaitAsync(Timeout);
                await cut.GetStream().WriteAsync("GET / HTTP/1.1\r\nHost: 127.0.0.1\r\n"u8.ToArray()).AsTask().WaitAsync(Timeout);
            }

            using var client = new ObsClient();
            var connect = client.ConnectAsync(hostname: "127.0.0.1", port: server.Port);
            await server.HandshakeAsync().WithTimeoutAsync(Timeout);

            Assert.True(await connect.WithTimeoutAsync(Timeout));
        }
    }
}
