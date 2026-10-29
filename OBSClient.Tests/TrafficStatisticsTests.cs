namespace OBSStudioClient.Tests
{
    using Microsoft.Extensions.Logging;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Tests.TestSupport;
    using System.Collections.Concurrent;
    using System.Text;
    using System.Text.Json;
    using Xunit;

    /// <summary>
    /// The counters of the traffic between the client and OBS Studio (<see cref="ObsClient.TotalBytesSent"/> and the others), and the
    /// events that report them. The fake server knows what it sent and what it received, so it can say what each counter has to be.
    /// No test read one of them before, although 4.0.0 fixed a counter that counted WebSocket frames instead of messages.
    /// </summary>
    public class TrafficStatisticsTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private static async Task ConnectAsync(ObsClient client, FakeObsWebSocketServer server)
        {
            var connect = client.ConnectAsync(hostname: "127.0.0.1", port: server.Port);
            await server.HandshakeAsync().WithTimeoutAsync(Timeout);
            Assert.True(await connect.WithTimeoutAsync(Timeout));
        }

        private static string Answer(string requestType, string requestId, string responseData) =>
            $"{{\"op\":7,\"d\":{{\"requestType\":\"{requestType}\",\"requestId\":\"{requestId}\",\"requestStatus\":{{\"result\":true,\"code\":100}},\"responseData\":{responseData}}}}}";

        // Makes a request, and hands the server the request that arrived, so that a test can answer it as it likes.
        private static async Task<(Task<Responses.InputSettingsResponse> Call, string RequestText, string RequestType, string RequestId)> RequestAsync(
            ObsClient client, FakeObsWebSocketServer server)
        {
            var call = client.GetInputSettings("some input");
            using var request = await server.ReceiveJsonAsync(Timeout);
            var data = request.RootElement.GetProperty("d");
            return (call, request.RootElement.GetRawText(), data.GetProperty("requestType").GetString()!, data.GetProperty("requestId").GetString()!);
        }

        [Fact]
        public async Task TheCounters_CountTheMessagesAndTheBytesOfEachDirection()
        {
            await using var server = new FakeObsWebSocketServer();
            server.Start();
            using var client = new ObsClient { RequestTimeout = 5000 };
            await ConnectAsync(client, server);

            // The handshake: the client sent its Identify, and read a Hello and an Identified.
            Assert.Equal(1, client.TotalMessagesSent);
            Assert.Equal(2, client.TotalMessagesReceived);
            Assert.Equal(1, client.SessionMessagesSent);
            Assert.Equal(2, client.SessionMessagesReceived);
            Assert.True(client.TotalBytesSent > 0);
            Assert.True(client.TotalBytesReceived > 0);
            Assert.Equal(client.TotalBytesSent, client.SessionBytesSent);
            Assert.Equal(client.TotalBytesReceived, client.SessionBytesReceived);
            var sentBefore = client.TotalBytesSent;
            var receivedBefore = client.TotalBytesReceived;

            var (call, requestText, requestType, requestId) = await RequestAsync(client, server);
            var answer = Answer(requestType, requestId, "{\"inputKind\":\"text_ft2_source_v2\",\"inputSettings\":{}}");
            await server.SendRawAsync(answer);
            await call.WithTimeoutAsync(Timeout);

            // What was read is counted before the request that it answers completes.
            Assert.Equal(3, client.TotalMessagesReceived);
            Assert.Equal(3, client.SessionMessagesReceived);
            Assert.Equal(receivedBefore + Encoding.UTF8.GetByteCount(answer), client.TotalBytesReceived);
            Assert.Equal(receivedBefore + Encoding.UTF8.GetByteCount(answer), client.SessionBytesReceived);

            // What was written is counted when the write is done, which can be after the answer was read.
            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => client.TotalMessagesSent == 2, Timeout));
            Assert.Equal(2, client.SessionMessagesSent);
            Assert.Equal(sentBefore + Encoding.UTF8.GetByteCount(requestText), client.TotalBytesSent);
            Assert.Equal(sentBefore + Encoding.UTF8.GetByteCount(requestText), client.SessionBytesSent);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        public async Task AMessageThatArrivesInSeveralPieces_IsCountedOnce(int frames)
        {
            // The client reads at most 4096 bytes at a time, so a message of 20 KB takes several reads even when it is one frame. Until 4.0.0 each
            // read counted as a message.
            await using var server = new FakeObsWebSocketServer();
            server.Start();
            using var client = new ObsClient { RequestTimeout = 5000 };
            await ConnectAsync(client, server);
            var messagesBefore = client.TotalMessagesReceived;
            var bytesBefore = client.TotalBytesReceived;

            var (call, _, requestType, requestId) = await RequestAsync(client, server);
            var text = new string('x', 20000);
            var answer = Answer(requestType, requestId, "{\"inputKind\":\"text_ft2_source_v2\",\"inputSettings\":{\"text\":\"" + text + "\"}}");
            await server.SendFragmentedAsync(answer, frames);
            var response = await call.WithTimeoutAsync(Timeout);

            Assert.Equal(text, ((JsonElement)response.InputSettings["text"]).GetString());
            Assert.Equal(messagesBefore + 1, client.TotalMessagesReceived);
            Assert.Equal(messagesBefore + 1, client.SessionMessagesReceived);
            Assert.Equal(bytesBefore + Encoding.UTF8.GetByteCount(answer), client.TotalBytesReceived);
        }

        [Fact]
        public async Task TheSessionCounters_StartOverWithEachConnection_AndTheTotalsKeepCounting()
        {
            await using var server = new FakeObsWebSocketServer();
            server.Start();
            using var client = new ObsClient();
            await ConnectAsync(client, server);
            var (sent, received, bytesSent, bytesReceived) = (client.TotalMessagesSent, client.TotalMessagesReceived, client.TotalBytesSent, client.TotalBytesReceived);

            client.Disconnect();
            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => client.ConnectionState == ConnectionState.Disconnected, Timeout));

            // The statistics are reset before the disconnect is reported.
            Assert.Equal(0, client.SessionMessagesSent);
            Assert.Equal(0, client.SessionMessagesReceived);
            Assert.Equal(0, client.SessionBytesSent);
            Assert.Equal(0, client.SessionBytesReceived);
            Assert.Equal(sent, client.TotalMessagesSent);
            Assert.Equal(received, client.TotalMessagesReceived);
            Assert.Equal(bytesSent, client.TotalBytesSent);
            Assert.Equal(bytesReceived, client.TotalBytesReceived);

            await ConnectAsync(client, server);

            Assert.Equal(1, client.SessionMessagesSent);
            Assert.Equal(2, client.SessionMessagesReceived);
            Assert.Equal(sent + 1, client.TotalMessagesSent);
            Assert.Equal(received + 2, client.TotalMessagesReceived);
            Assert.True(client.SessionBytesSent > 0);
            Assert.True(client.TotalBytesSent > bytesSent);
            Assert.True(client.TotalBytesReceived > bytesReceived);
        }

        [Fact]
        public async Task TheStatisticsEvents_ReportTheNewValueOfTheirCounter()
        {
            await using var server = new FakeObsWebSocketServer();
            server.Start();
            using var client = new ObsClient();
            var raised = new ConcurrentQueue<(string Counter, long Value)>();
            client.TotalBytesSentChanged += (_, value) => raised.Enqueue(("TotalBytesSent", value));
            client.TotalBytesReceivedChanged += (_, value) => raised.Enqueue(("TotalBytesReceived", value));
            client.TotalMessagesSentChanged += (_, value) => raised.Enqueue(("TotalMessagesSent", value));
            client.TotalMessagesReceivedChanged += (_, value) => raised.Enqueue(("TotalMessagesReceived", value));
            client.SessionBytesSentChanged += (_, value) => raised.Enqueue(("SessionBytesSent", value));
            client.SessionBytesReceivedChanged += (_, value) => raised.Enqueue(("SessionBytesReceived", value));
            client.SessionMessagesSentChanged += (_, value) => raised.Enqueue(("SessionMessagesSent", value));
            client.SessionMessagesReceivedChanged += (_, value) => raised.Enqueue(("SessionMessagesReceived", value));

            await ConnectAsync(client, server);

            long[] Values(string counter) => [.. raised.Where(entry => entry.Counter == counter).Select(entry => entry.Value)];

            // One event for each message, with the number of messages so far: the client sent its Identify, and read a Hello and an Identified.
            Assert.Equal([1L], Values("TotalMessagesSent"));
            Assert.Equal([1L], Values("SessionMessagesSent"));
            Assert.Equal([1L, 2L], Values("TotalMessagesReceived"));
            Assert.Equal([1L, 2L], Values("SessionMessagesReceived"));

            // The bytes are reported for every read, so more than one value comes by, and the last is what the counter says now.
            var counters = new (string Name, long Now)[]
            {
                ("TotalBytesSent", client.TotalBytesSent),
                ("TotalBytesReceived", client.TotalBytesReceived),
                ("SessionBytesSent", client.SessionBytesSent),
                ("SessionBytesReceived", client.SessionBytesReceived),
            };
            foreach (var (name, now) in counters)
            {
                var values = Values(name);
                Assert.NotEmpty(values);
                Assert.Equal(now, values[^1]);
                Assert.Equal(values.Order(), values);
            }
        }

        [Fact]
        public async Task AStatisticsHandlerThatThrows_DoesNotDisturbTheConnection()
        {
            await using var server = new FakeObsWebSocketServer();
            server.Start();
            var logger = new ListLogger();
            using var client = new ObsClient(logger) { RequestTimeout = 5000 };
            client.TotalBytesSentChanged += (_, _) => throw new InvalidOperationException("a handler that fails");
            client.TotalBytesReceivedChanged += (_, _) => throw new InvalidOperationException("a handler that fails");
            client.TotalMessagesSentChanged += (_, _) => throw new InvalidOperationException("a handler that fails");
            client.TotalMessagesReceivedChanged += (_, _) => throw new InvalidOperationException("a handler that fails");
            client.SessionBytesSentChanged += (_, _) => throw new InvalidOperationException("a handler that fails");
            client.SessionBytesReceivedChanged += (_, _) => throw new InvalidOperationException("a handler that fails");
            client.SessionMessagesSentChanged += (_, _) => throw new InvalidOperationException("a handler that fails");
            client.SessionMessagesReceivedChanged += (_, _) => throw new InvalidOperationException("a handler that fails");

            await ConnectAsync(client, server);
            var (call, _, requestType, requestId) = await RequestAsync(client, server);
            await server.SendRawAsync(Answer(requestType, requestId, "{\"inputKind\":\"text_ft2_source_v2\",\"inputSettings\":{}}"));
            await call.WithTimeoutAsync(Timeout, "The connection stopped working after a statistics handler threw.");

            Assert.Equal(ConnectionState.Connected, client.ConnectionState);
            Assert.Equal(3, client.TotalMessagesReceived);
            Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Error && entry.Message.Contains("statistics event handler", StringComparison.Ordinal));
        }
    }
}
