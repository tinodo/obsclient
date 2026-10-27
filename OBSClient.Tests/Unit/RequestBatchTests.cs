namespace OBSStudioClient.Tests.Unit
{
    using System.Text.Json;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Messages;
    using OBSStudioClient.Tests.TestSupport;

    /// <summary>
    /// The request batch as OBS Studio receives it, and as it answers. obs-websocket reads haltOnFailure, executionType and
    /// requests from the batch by those names and ignores a field it does not know, so a field with the wrong name does not
    /// fail: it silently does nothing.
    /// </summary>
    public class RequestBatchTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        [Theory]
        [InlineData(true, RequestBatchExecutionType.SerialRealtime, 0)]
        [InlineData(true, RequestBatchExecutionType.SerialFrame, 1)]
        [InlineData(false, RequestBatchExecutionType.Parallel, 2)]
        public async Task ABatch_IsSent_WithTheFieldsThatObsStudioReads(bool haltOnFailure, RequestBatchExecutionType executionType, int number)
        {
            await using var server = new FakeObsWebSocketServer();
            using var client = await ConnectAsync(server);
            var batch = new RequestBatchMessage(haltOnFailure, executionType);
            batch.AddSetInputMuteRequest("Microphone", true);
            batch.AddSleepRequest(100, null);

            var sending = client.SendRequestBatchAsync(batch);
            using var message = await server.ReceiveJsonAsync(Timeout);

            Assert.Equal(8, message.RootElement.GetProperty("op").GetInt32());
            var data = message.RootElement.GetProperty("d");
            Assert.Equal(
                new[] { "executionType", "haltOnFailure", "requestId", "requests" },
                data.EnumerateObject().Select(member => member.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray());
            Assert.Equal(batch.RequestId, data.GetProperty("requestId").GetString());
            Assert.Equal(haltOnFailure, data.GetProperty("haltOnFailure").GetBoolean());
            Assert.Equal(number, data.GetProperty("executionType").GetInt32());

            var requests = data.GetProperty("requests");
            Assert.Equal(2, requests.GetArrayLength());
            Assert.Equal("SetInputMute", requests[0].GetProperty("requestType").GetString());
            Assert.Equal("Microphone", requests[0].GetProperty("requestData").GetProperty("inputName").GetString());
            Assert.Equal("Sleep", requests[1].GetProperty("requestType").GetString());
            Assert.Equal(100, requests[1].GetProperty("requestData").GetProperty("sleepMillis").GetInt32());

            await AnswerAsync(server, batch.RequestId, requests, requests.GetArrayLength());
            var responses = await sending.WithTimeoutAsync(Timeout);

            Assert.Equal(new[] { RequestType.SetInputMute, RequestType.Sleep }, responses.Select(response => response.RequestType).ToArray());
            Assert.All(responses, response => Assert.True(response.RequestStatus.Result));
        }

        [Fact]
        public async Task ABatchThatHalted_ReturnsOnlyTheRequestsThatWereProcessed_TheLastOneFailed()
        {
            await using var server = new FakeObsWebSocketServer();
            using var client = await ConnectAsync(server);
            var batch = new RequestBatchMessage(haltOnFailure: true);
            batch.AddSetInputMuteRequest("Microphone", true);
            batch.AddSetInputMuteRequest("No such input", true);
            batch.AddStartRecordRequest();

            var sending = client.SendRequestBatchAsync(batch);
            using var message = await server.ReceiveJsonAsync(Timeout);
            var requests = message.RootElement.GetProperty("d").GetProperty("requests");
            Assert.Equal(3, requests.GetArrayLength());

            var results = new[]
            {
                new { requestType = "SetInputMute", requestId = requests[0].GetProperty("requestId").GetString(), requestStatus = new { result = true, code = 100, comment = (string?)null } },
                new { requestType = "SetInputMute", requestId = requests[1].GetProperty("requestId").GetString(), requestStatus = new { result = false, code = 600, comment = (string?)"No input was found by the name of No such input." } },
            };
            await server.SendRawAsync(JsonSerializer.Serialize(new { op = 9, d = new { requestId = batch.RequestId, results } }));
            var responses = await sending.WithTimeoutAsync(Timeout);

            Assert.Equal(2, responses.Length);
            Assert.True(responses[0].RequestStatus.Result);
            Assert.False(responses[1].RequestStatus.Result);
            Assert.Equal(RequestStatusCode.ResourceNotFound, responses[1].RequestStatus.Code);
            Assert.Equal("No input was found by the name of No such input.", responses[1].RequestStatus.Comment);
        }

        private static async Task<ObsClient> ConnectAsync(FakeObsWebSocketServer server)
        {
            server.Start();
            var client = new ObsClient { RequestTimeout = 5000 };
            var connect = client.ConnectAsync(hostname: "127.0.0.1", port: server.Port);
            await server.HandshakeAsync().WithTimeoutAsync(Timeout);
            await connect.WithTimeoutAsync(Timeout);
            return client;
        }

        private static Task AnswerAsync(FakeObsWebSocketServer server, string batchId, JsonElement requests, int count)
        {
            var results = requests.EnumerateArray().Take(count).Select(request => new
            {
                requestType = request.GetProperty("requestType").GetString(),
                requestId = request.GetProperty("requestId").GetString(),
                requestStatus = new { result = true, code = 100 },
            }).ToArray();
            return server.SendRawAsync(JsonSerializer.Serialize(new { op = 9, d = new { requestId = batchId, results } }));
        }
    }
}
