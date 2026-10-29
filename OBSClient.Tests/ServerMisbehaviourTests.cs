namespace OBSStudioClient.Tests
{
    using Microsoft.Extensions.Logging;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Messages;
    using OBSStudioClient.Tests.TestSupport;
    using System.Diagnostics;
    using Xunit;

    /// <summary>
    /// A server that does not keep to the protocol: what it sends that the client cannot make sense of, and what it never sends. The client
    /// has to answer each with a failure that says what happened, as soon as it can know, and must not be left in a state that it cannot
    /// leave: a call that is answered wrongly fails at once (the request timeout of these tests is a minute), a message that cannot be read
    /// is logged and skipped, and a handshake that cannot be completed ends the attempt to connect.
    /// </summary>
    public class ServerMisbehaviourTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private static async Task<ObsClient> ConnectAsync(FakeObsWebSocketServer server, ILogger<ObsClient>? logger = null)
        {
            server.Start();
            var client = logger is null ? new ObsClient() : new ObsClient(logger);
            client.RequestTimeout = 60000;
            var connect = client.ConnectAsync(hostname: "127.0.0.1", port: server.Port);
            await server.HandshakeAsync().WithTimeoutAsync(Timeout);
            await connect.WithTimeoutAsync(Timeout);
            return client;
        }

        // A call that is not answered with what it needs fails right away: waiting for the timeout would be the bug.
        private static async Task<TException> FailsAtOnceAsync<TException>(Task call)
            where TException : Exception
        {
            var finished = await Task.WhenAny(call, Task.Delay(Timeout));
            Assert.True(ReferenceEquals(call, finished), "The call was still waiting for its timeout, although it was answered.");
            return await Assert.ThrowsAsync<TException>(() => call);
        }

        private static async Task AssertStillUsableAsync(ObsClient client, FakeObsWebSocketServer server)
        {
            var call = client.GetSceneList();
            var (requestType, requestId, _) = await server.ReceiveRequestAsync(Timeout);
            await server.SendRequestSuccessAsync(requestType, requestId, new { scenes = Array.Empty<object>() });

            var response = await call.WithTimeoutAsync(Timeout, "The connection stopped working.");

            Assert.Empty(response.Scenes);
            Assert.Equal(ConnectionState.Connected, client.ConnectionState);
        }

        [Fact]
        public async Task ABatchThatIsAnsweredLikeASingleRequest_FailsAtOnce()
        {
            await using var server = new FakeObsWebSocketServer();
            using var client = await ConnectAsync(server);
            var batch = new RequestBatchMessage();
            batch.AddSleepRequest(10, null);

            var call = client.SendRequestBatchAsync(batch);
            using var received = await server.ReceiveJsonAsync(Timeout);
            await server.SendRawAsync("{\"op\":7,\"d\":{\"requestType\":\"Sleep\",\"requestId\":\"" + batch.RequestId + "\",\"requestStatus\":{\"result\":true,\"code\":100}}}");

            var exception = await FailsAtOnceAsync<ObsClientException>(call);

            Assert.Contains("Unexpected response type", exception.Message, StringComparison.Ordinal);
            await AssertStillUsableAsync(client, server);
        }

        [Fact]
        public async Task ASingleRequestThatIsAnsweredLikeABatch_FailsAtOnce()
        {
            await using var server = new FakeObsWebSocketServer();
            using var client = await ConnectAsync(server);

            var call = client.GetSceneList();
            var (_, requestId, _) = await server.ReceiveRequestAsync(Timeout);
            await server.SendRawAsync("{\"op\":9,\"d\":{\"requestId\":\"" + requestId + "\",\"results\":[]}}");

            var exception = await FailsAtOnceAsync<ObsClientException>(call);

            Assert.Contains("Unexpected response type", exception.Message, StringComparison.Ordinal);
            await AssertStillUsableAsync(client, server);
        }

        [Theory]
        [InlineData(99)]
        [InlineData(6)]
        [InlineData(1)]
        public async Task AMessageWithAnOpCodeThatTheClientNeverReceives_IsLoggedAndSkipped(int op)
        {
            await using var server = new FakeObsWebSocketServer();
            var logger = new ListLogger();
            using var client = await ConnectAsync(server, logger);

            await server.SendRawAsync("{\"op\":" + op + ",\"d\":{}}");

            await AssertStillUsableAsync(client, server);
            Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Error && entry.Message.Contains("could not be read and is ignored", StringComparison.Ordinal));
        }

        [Theory]
        [InlineData("not json at all")]
        [InlineData("null")]
        [InlineData("[1,2,3]")]
        [InlineData("\"text\"")]
        [InlineData("{}")]
        public async Task AFrameThatIsNotAMessage_IsLoggedAndSkipped(string frame)
        {
            await using var server = new FakeObsWebSocketServer();
            var logger = new ListLogger();
            using var client = await ConnectAsync(server, logger);

            await server.SendRawAsync(frame);

            await AssertStillUsableAsync(client, server);
            Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Error && entry.Message.Contains("could not be read and is ignored", StringComparison.Ordinal));
        }

        // Found by the fuzz tests: the client looks for the op code of a message that it cannot read, to tell what it was the response to,
        // and it asked a string, a boolean, an array and an object for an integer, which throws; that closed the connection.
        [Theory]
        [InlineData("{\"op\":\"5\",\"d\":{}}")]
        [InlineData("{\"op\":null,\"d\":{}}")]
        [InlineData("{\"op\":true,\"d\":{}}")]
        [InlineData("{\"op\":[5],\"d\":{}}")]
        [InlineData("{\"op\":{},\"d\":\"y5]T\"}")]
        [InlineData("{\"op\":5.5,\"d\":{}}")]
        [InlineData("{\"op\":4294967296,\"d\":{}}")]
        public async Task AFrameWhoseOpCodeIsNotAnInteger_IsLoggedAndSkipped(string frame)
        {
            await using var server = new FakeObsWebSocketServer();
            var logger = new ListLogger();
            using var client = await ConnectAsync(server, logger);

            await server.SendRawAsync(frame);

            await AssertStillUsableAsync(client, server);
            Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Error && entry.Message.Contains("could not be read and is ignored", StringComparison.Ordinal));
        }

        // Found by the fuzz tests: a message without an op code was read as op code 0, which is a Hello, and a message whose data is null was
        // read as a message without data; the client then did not know what to do with it, and that ended the connection. Neither is a message.
        [Theory]
        [InlineData("{\"d\":null}")]
        [InlineData("{\"d\":{}}")]
        [InlineData("{\"d\":{\"rpcVersion\":1}}")]
        [InlineData("{\"op\":5,\"d\":null}")]
        [InlineData("{\"op\":7,\"d\":null}")]
        [InlineData("{\"op\":9,\"d\":null}")]
        [InlineData("{\"op\":5}")]
        [InlineData("{\"op\":7}")]
        [InlineData("{\"op\":9}")]
        public async Task AMessageWithoutAnOpCodeOrWithoutData_IsLoggedAndSkipped(string frame)
        {
            await using var server = new FakeObsWebSocketServer();
            var logger = new ListLogger();
            using var client = await ConnectAsync(server, logger);

            await server.SendRawAsync(frame);

            await AssertStillUsableAsync(client, server);
            Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Error && entry.Message.Contains("could not be read and is ignored", StringComparison.Ordinal));
        }

        // A response that does not say which request it answers: it cannot be given to anyone, and it must not take the connection with it
        // (found by the fuzz tests: the missing ID reached a dictionary as a null key, which throws, and that ended the connection).
        [Theory]
        [InlineData("{\"op\":7,\"d\":{\"requestType\":\"GetSceneList\",\"requestStatus\":{\"result\":true,\"code\":100},\"responseData\":{\"scenes\":[]}}}")]
        [InlineData("{\"op\":7,\"d\":{\"requestType\":\"GetSceneList\",\"requestId\":null,\"requestStatus\":{\"result\":true,\"code\":100},\"responseData\":{\"scenes\":[]}}}")]
        [InlineData("{\"op\":9,\"d\":{\"results\":[]}}")]
        [InlineData("{\"op\":9,\"d\":{\"requestId\":null,\"results\":[]}}")]
        public async Task AResponseWithoutTheIdOfARequest_IsLoggedAndSkipped(string frame)
        {
            await using var server = new FakeObsWebSocketServer();
            var logger = new ListLogger();
            using var client = await ConnectAsync(server, logger);

            await server.SendRawAsync(frame);

            await AssertStillUsableAsync(client, server);
            Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("without the ID of the request", StringComparison.Ordinal));
        }

        [Fact]
        public async Task AnEmptyFrame_IsSkipped()
        {
            await using var server = new FakeObsWebSocketServer();
            var logger = new ListLogger();
            using var client = await ConnectAsync(server, logger);

            await server.SendRawAsync(string.Empty);

            await AssertStillUsableAsync(client, server);
            Assert.DoesNotContain(logger.Entries, entry => entry.Level >= LogLevel.Warning);
        }

        [Fact]
        public async Task AHelloThatCannotBeRead_EndsTheAttemptToConnect()
        {
            await using var server = new FakeObsWebSocketServer();
            server.Start();
            var logger = new ListLogger();
            using var client = new ObsClient(logger);
            var closed = new EventAwaiter<ConnectionClosedEventArgs>();
            client.ConnectionClosed += closed.Handler;

            var connect = client.ConnectAsync(hostname: "127.0.0.1", port: server.Port);
            await server.WaitForConnectionAsync(Timeout);
            await server.SendRawAsync("{\"op\":0,\"d\":{\"rpcVersion\":\"not a number\"}}");

            Assert.False(await connect.WithTimeoutAsync(Timeout, "ConnectAsync was still waiting after OBS Studio sent a hello that cannot be read."));
            await closed.Task.WithTimeoutAsync(Timeout, "ConnectionClosed was not raised.");
            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => client.ConnectionState == ConnectionState.Disconnected, Timeout));
            Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Error && entry.Message.Contains("receiver loop", StringComparison.Ordinal));
        }

        [Fact]
        public async Task AnIdentifiedThatCannotBeRead_EndsTheAttemptToConnect()
        {
            await using var server = new FakeObsWebSocketServer();
            server.Start();
            var logger = new ListLogger();
            using var client = new ObsClient(logger);

            var connect = client.ConnectAsync(hostname: "127.0.0.1", port: server.Port);
            await server.WaitForConnectionAsync(Timeout);
            await server.SendRawAsync("{\"op\":0,\"d\":{\"obsWebSocketVersion\":\"5.7.6\",\"rpcVersion\":1}}");
            using var identify = await server.ReceiveJsonAsync(Timeout);
            Assert.Equal(1, identify.RootElement.GetProperty("op").GetInt32());
            await server.SendRawAsync("{\"op\":2,\"d\":{\"negotiatedRpcVersion\":\"not a number\"}}");

            Assert.False(await connect.WithTimeoutAsync(Timeout, "ConnectAsync was still waiting after OBS Studio sent an identified that cannot be read."));
            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => client.ConnectionState == ConnectionState.Disconnected, Timeout));

            // The attempt ended because the unreadable message was fatal, and not because the handshake ran out of time (which is also false,
            // after ten seconds, and logs a warning instead): an unreadable Identified that is ignored would only be told apart by this.
            Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Error && entry.Message.Contains("receiver loop", StringComparison.Ordinal));
            Assert.DoesNotContain(logger.Entries, entry => entry.Message.Contains("did not complete the authentication handshake", StringComparison.Ordinal));
        }

        [Fact]
        public async Task AHandshakeThatIsNeverCompleted_IsGivenUpAfterTenSeconds()
        {
            // Something accepted the connection and never said hello: ConnectAsync is documented to return false after 10 seconds.
            await using var server = new FakeObsWebSocketServer();
            server.Start();
            var logger = new ListLogger();
            using var client = new ObsClient(logger);

            var watch = Stopwatch.StartNew();
            var connect = client.ConnectAsync(hostname: "127.0.0.1", port: server.Port);
            await server.WaitForConnectionAsync(Timeout);
            var connected = await connect.WithTimeoutAsync(TimeSpan.FromSeconds(40), "ConnectAsync never gave up on a handshake that was not completed.");
            watch.Stop();

            Assert.False(connected);
            Assert.InRange(watch.Elapsed.TotalSeconds, 9, 35);
            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => client.ConnectionState == ConnectionState.Disconnected, Timeout));
            Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("did not complete the authentication handshake", StringComparison.Ordinal));
        }
    }
}
