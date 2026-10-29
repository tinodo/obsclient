namespace OBSStudioClient.Tests
{
    using Microsoft.Extensions.Logging;
    using OBSStudioClient.Tests.TestSupport;
    using System.Collections.Concurrent;
    using Xunit;

    /// <summary>
    /// When the handlers of an application are slower than OBS Studio is, the events queue up in memory without a limit, because dropping
    /// one would lose a change that the application has to see. The client warns when a thousand of them are waiting.
    /// </summary>
    public class EventBacklogTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

        [Fact]
        public async Task WhenTheHandlersAreSlowerThanObsStudio_NoEventIsLost_TheyStayInOrder_AndTheClientWarns()
        {
            const int count = 1100;
            await using var server = new FakeObsWebSocketServer();
            server.Start();
            var logger = new ListLogger();
            using var client = new ObsClient(logger);
            using var gate = new ManualResetEventSlim();
            var names = new ConcurrentQueue<string>();
            client.SceneCreated += (_, scene) =>
            {
                names.Enqueue(scene.SceneName);
                if (names.Count == 1)
                {
                    // The first handler is slow, so that every event that comes after it has to wait.
                    gate.Wait(Timeout);
                }
            };
            var connect = client.ConnectAsync(hostname: "127.0.0.1", port: server.Port);
            await server.HandshakeAsync().WithTimeoutAsync(Timeout);
            await connect.WithTimeoutAsync(Timeout);

            for (var index = 0; index < count; index++)
            {
                await server.SendEventAsync("SceneCreated", new { sceneName = $"Scene {index}", sceneUuid = Guid.NewGuid(), isGroup = false });
            }

            Assert.True(
                await AsyncTestHelpers.WaitUntilAsync(
                    () => logger.Entries.Any(entry => entry.Level == LogLevel.Warning && entry.Message.Contains("notifications are waiting", StringComparison.Ordinal)),
                    Timeout),
                "The client did not warn that a thousand events were waiting for a handler.");
            Assert.True(names.Count <= 1, "A handler ran for a second event while the first one still held up the others.");

            gate.Set();

            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => names.Count == count, Timeout), $"Only {names.Count} of {count} events were raised.");
            Assert.Equal(Enumerable.Range(0, count).Select(index => $"Scene {index}"), names);
        }
    }
}
