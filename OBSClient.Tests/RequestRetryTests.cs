namespace OBSStudioClient.Tests
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Tests.TestSupport;
    using Xunit;

    /// <summary>
    /// OBS Studio answers "not ready" (code 207) to a request that it cannot perform yet, for instance while it is still starting up and
    /// has authenticated the client already. The client asks again, as often as <see cref="ObsClient.MaxRequestRetries"/> allows and
    /// <see cref="ObsClient.RequestRetryInterval"/> apart; the settings are checked here too.
    /// </summary>
    public class RequestRetryTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private const int NotReady = 207;

        private static async Task<ObsClient> ConnectAsync(FakeObsWebSocketServer server)
        {
            server.Start();
            var client = new ObsClient { RequestTimeout = 5000, RequestRetryInterval = 10 };
            var connect = client.ConnectAsync(hostname: "127.0.0.1", port: server.Port);
            await server.HandshakeAsync().WithTimeoutAsync(Timeout);
            await connect.WithTimeoutAsync(Timeout);
            return client;
        }

        [Fact]
        public async Task ARequestThatIsNotReady_IsAskedAgain_UntilObsStudioIsReady()
        {
            await using var server = new FakeObsWebSocketServer();
            using var client = await ConnectAsync(server);

            var call = client.GetSceneList();
            var ids = new List<string>();
            for (var attempt = 0; attempt < 3; attempt++)
            {
                var (requestType, requestId, _) = await server.ReceiveRequestAsync(Timeout);
                ids.Add(requestId);
                if (attempt < 2)
                {
                    await server.SendRequestErrorAsync(requestType, requestId, NotReady, "OBS Studio is not ready.");
                }
                else
                {
                    await server.SendRequestSuccessAsync(requestType, requestId, new { scenes = Array.Empty<object>() });
                }
            }

            var response = await call.WithTimeoutAsync(Timeout);

            Assert.Empty(response.Scenes);
            Assert.Equal(3, ids.Distinct().Count());
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(3)]
        public async Task ARequestThatStaysNotReady_FailsWhenTheRetriesThatAreAllowedAreUsed(int retries)
        {
            await using var server = new FakeObsWebSocketServer();
            using var client = await ConnectAsync(server);
            client.MaxRequestRetries = retries;

            var call = client.GetSceneList();
            for (var attempt = 0; attempt <= retries; attempt++)
            {
                var (requestType, requestId, _) = await server.ReceiveRequestAsync(Timeout);
                await server.SendRequestErrorAsync(requestType, requestId, NotReady, "OBS Studio is not ready.");
            }

            var exception = await Assert.ThrowsAsync<ObsResponseException>(() => call.WithTimeoutAsync(Timeout));
            Assert.Equal(RequestStatusCode.NotReady, exception.ErrorCode);

            // The first request and the retries that were allowed: nothing is asked beyond that.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => server.ReceiveRequestAsync(TimeSpan.FromMilliseconds(300)));
        }

        [Fact]
        public async Task AnotherErrorThanNotReady_IsNotRetried()
        {
            await using var server = new FakeObsWebSocketServer();
            using var client = await ConnectAsync(server);

            var call = client.GetSceneList();
            var (requestType, requestId, _) = await server.ReceiveRequestAsync(Timeout);
            await server.SendRequestErrorAsync(requestType, requestId, 600, "No such scene.");

            var exception = await Assert.ThrowsAsync<ObsResponseException>(() => call.WithTimeoutAsync(Timeout));
            Assert.Equal(RequestStatusCode.ResourceNotFound, exception.ErrorCode);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => server.ReceiveRequestAsync(TimeSpan.FromMilliseconds(300)));
        }

        [Fact]
        public void TheSettingsOfTheRetries_AreCheckedAndReported()
        {
            using var client = new ObsClient();
            var changed = new List<string?>();
            client.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
            Assert.Equal(10, client.MaxRequestRetries);
            Assert.Equal(500, client.RequestRetryInterval);
            Assert.Equal(500, client.RequestTimeout);

            client.MaxRequestRetries = 3;
            client.RequestRetryInterval = 20;
            client.RequestTimeout = 2000;

            Assert.Equal(["MaxRequestRetries", "RequestRetryInterval", "RequestTimeout"], changed);

            // The value that is there already, and a value that is not allowed (a negative number), change and report nothing.
            client.MaxRequestRetries = 3;
            client.MaxRequestRetries = -1;
            client.RequestRetryInterval = 20;
            client.RequestRetryInterval = -5;
            Assert.Equal(3, client.MaxRequestRetries);
            Assert.Equal(20, client.RequestRetryInterval);
            Assert.Equal(3, changed.Count);

            // A timeout below the minimum is raised to it.
            client.RequestTimeout = 10;
            Assert.Equal(150, client.RequestTimeout);
            Assert.Equal(4, changed.Count);
            client.RequestTimeout = 1;
            Assert.Equal(150, client.RequestTimeout);
            Assert.Equal(4, changed.Count);
        }
    }
}
