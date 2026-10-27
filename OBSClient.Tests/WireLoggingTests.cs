namespace OBSStudioClient.Tests
{
    using Microsoft.Extensions.Logging;
    using OBSStudioClient.Tests.TestSupport;

    /// <summary>
    /// The test app (OBSClient.TestApp) builds its wire log from the debug messages "Sending: {json}" and "Received: {json}"
    /// that the client logs, so those messages are a contract, and the answer to the authentication challenge stays out of them.
    /// </summary>
    public class WireLoggingTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        [Fact]
        public async Task Client_LogsTheMessagesThatGoOverTheWire_AtTheDebugLevel()
        {
            var log = new ListLogger();
            await using var server = new FakeObsWebSocketServer();
            server.Start();
            using var client = new ObsClient(log) { RequestTimeout = 5000 };

            var connect = client.ConnectAsync(hostname: "127.0.0.1", port: server.Port);
            await server.HandshakeAsync().WithTimeoutAsync(Timeout);
            await connect.WithTimeoutAsync(Timeout);

            var call = client.SetCurrentProgramScene("Scene");
            var (requestType, requestId, _) = await server.ReceiveRequestAsync(Timeout);
            await server.SendRequestSuccessAsync(requestType, requestId);
            await call.WithTimeoutAsync(Timeout);

            Assert.Contains(
                log.Entries,
                entry => entry.Level == LogLevel.Debug
                    && entry.Message.StartsWith("Sending: ", StringComparison.Ordinal)
                    && entry.Message.Contains("\"requestType\":\"SetCurrentProgramScene\"", StringComparison.Ordinal));
            Assert.Contains(
                log.Entries,
                entry => entry.Level == LogLevel.Debug
                    && entry.Message.StartsWith("Received: ", StringComparison.Ordinal)
                    && entry.Message.Contains("\"requestType\":\"SetCurrentProgramScene\"", StringComparison.Ordinal));
        }

        [Fact]
        public async Task Client_DoesNotLogTheAnswerToTheAuthenticationChallenge()
        {
            const string password = "P@ssw0rd!";
            var log = new ListLogger();
            await using var server = new FakeObsWebSocketServer();
            server.Start();
            using var client = new ObsClient(log);

            var connect = client.ConnectAsync(password: password, hostname: "127.0.0.1", port: server.Port);
            using var identify = await server.HandshakeAsync(password: password).WithTimeoutAsync(Timeout);
            await connect.WithTimeoutAsync(Timeout);

            var authentication = identify.RootElement.GetProperty("d").GetProperty("authentication").GetString();
            Assert.False(string.IsNullOrEmpty(authentication));

            var sent = Assert.Single(
                log.Entries,
                entry => entry.Message.StartsWith("Sending: ", StringComparison.Ordinal) && entry.Message.Contains("\"op\":1", StringComparison.Ordinal));
            Assert.DoesNotContain(authentication!, sent.Message, StringComparison.Ordinal);
            Assert.Contains("\"authentication\":\"***\"", sent.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(log.Entries, entry => entry.Message.Contains(password, StringComparison.Ordinal));
        }
    }
}
