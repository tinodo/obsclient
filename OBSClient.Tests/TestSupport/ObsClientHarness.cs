namespace OBSStudioClient.Tests.TestSupport
{
    using System.Text.Json;

    /// <summary>
    /// A request as the fake OBS server received it.
    /// </summary>
    /// <param name="RequestType">The type of the request.</param>
    /// <param name="Data">The request data, if there was any.</param>
    internal sealed record SentRequest(string RequestType, JsonElement? Data)
    {
        /// <summary>
        /// Gets the names of the fields that the request carries, in alphabetical order.
        /// </summary>
        public string[] Fields => Data is { ValueKind: JsonValueKind.Object } data
            ? data.EnumerateObject().Select(member => member.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray()
            : [];

        /// <summary>
        /// Gets the value of a field of the request.
        /// </summary>
        /// <param name="name">The name of the field.</param>
        public JsonElement this[string name] => Data!.Value.GetProperty(name);
    }

    /// <summary>
    /// Runs one call of an <see cref="ObsClient"/> against a fake OBS server and reports what the client sent.
    /// </summary>
    internal static class ObsClientHarness
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Makes a call that returns nothing.
        /// </summary>
        /// <param name="call">The call to make.</param>
        /// <param name="response">The response data that the server answers with.</param>
        public static async Task<SentRequest> SendAsync(Func<ObsClient, Task> call, object? response = null)
        {
            await using var server = new FakeObsWebSocketServer();
            using var client = await ConnectAsync(server);

            var task = call(client);
            var sent = await AnswerAsync(server, response);
            await task.WithTimeoutAsync(Timeout);
            return sent;
        }

        /// <summary>
        /// Makes a call that returns a result.
        /// </summary>
        /// <typeparam name="T">The type of the result.</typeparam>
        /// <param name="call">The call to make.</param>
        /// <param name="response">The response data that the server answers with.</param>
        public static async Task<(SentRequest Sent, T Result)> QueryAsync<T>(Func<ObsClient, Task<T>> call, object? response = null)
        {
            await using var server = new FakeObsWebSocketServer();
            using var client = await ConnectAsync(server);

            var task = call(client);
            var sent = await AnswerAsync(server, response);
            var result = await task.WithTimeoutAsync(Timeout);
            return (sent, result);
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

        private static async Task<SentRequest> AnswerAsync(FakeObsWebSocketServer server, object? response)
        {
            var (requestType, requestId, data) = await server.ReceiveRequestAsync(Timeout);
            await server.SendRequestSuccessAsync(requestType, requestId, response);
            return new SentRequest(requestType, data is { ValueKind: JsonValueKind.Null } ? null : data);
        }
    }
}
