namespace OBSStudioClient.Tests
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Messages;
    using OBSStudioClient.Tests.TestSupport;
    using Xunit;

    /// <summary>
    /// What a caller gets when it uses the client in a state that does not allow it: no connection (never made, or lost, or ended by
    /// <see cref="ObsClient.Dispose"/>), a connection that is there already, a host name that cannot be one. Each fails at once and says why.
    /// </summary>
    public class ClientMisuseTests
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

        private static async Task AssertNotConnectedAsync(ObsClient client)
        {
            var batch = new RequestBatchMessage();
            batch.AddGetVersionRequest();

            var calls = new Func<Task>[]
            {
                () => client.GetSceneList(),
                () => client.SendRequestBatchAsync(batch),
                () => client.ReidentifyAsync(EventSubscriptions.All),
            };
            foreach (var call in calls)
            {
                var exception = await Assert.ThrowsAsync<ObsClientException>(call);
                Assert.Equal("Not connected.", exception.Message);
            }
        }

        [Theory]
        [InlineData("not a host name")]
        [InlineData("a b")]
        public async Task ConnectAsync_WithAnInvalidHostname_ThrowsAndChangesNothing(string hostname)
        {
            using var client = new ObsClient();

            var exception = await Assert.ThrowsAsync<ArgumentException>(() => client.ConnectAsync(hostname: hostname));

            Assert.Equal("hostname", exception.ParamName);
            Assert.Equal(ConnectionState.Disconnected, client.ConnectionState);
        }

        [Fact]
        public async Task ConnectAsync_WhenTheClientIsConnectedAlready_ReturnsTrue_AndOpensNothing()
        {
            await using var server = new FakeObsWebSocketServer();
            using var client = await ConnectAsync(server);

            // Another port, where nothing listens: it would fail if anything were tried.
            var again = await client.ConnectAsync(hostname: "127.0.0.1", port: 1);

            Assert.True(again);
            Assert.Equal(ConnectionState.Connected, client.ConnectionState);
            var call = client.GetSceneList();
            var (requestType, requestId, _) = await server.ReceiveRequestAsync(Timeout);
            await server.SendRequestSuccessAsync(requestType, requestId, new { scenes = Array.Empty<object>() });
            await call.WithTimeoutAsync(Timeout, "The connection stopped working after a second ConnectAsync.");
        }

        [Fact]
        public async Task AClientThatWasNeverConnected_FailsEveryCallAtOnce()
        {
            using var client = new ObsClient();

            await AssertNotConnectedAsync(client);
        }

        [Fact]
        public async Task AClientWhoseConnectionWasLost_FailsEveryCallAtOnce()
        {
            await using var server = new FakeObsWebSocketServer();
            using var client = await ConnectAsync(server);

            server.AbortConnection();
            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => client.ConnectionState == ConnectionState.Disconnected, Timeout));

            await AssertNotConnectedAsync(client);
        }

        [Fact]
        public async Task ADisposedClient_FailsEveryCallAtOnce()
        {
            await using var server = new FakeObsWebSocketServer();
            using var client = await ConnectAsync(server);

            client.Dispose();
            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => client.ConnectionState == ConnectionState.Disconnected, Timeout));

            await AssertNotConnectedAsync(client);
        }

        [Fact]
        public void Disconnect_AndDispose_AreHarmlessWhenNothingIsConnected_AndWhenCalledAgain()
        {
            var client = new ObsClient();

            client.Disconnect();
            client.Disconnect();
            client.Dispose();
            client.Dispose();
            client.Disconnect();

            Assert.Equal(ConnectionState.Disconnected, client.ConnectionState);
            Assert.False(client.AutoReconnect);
        }
    }
}
