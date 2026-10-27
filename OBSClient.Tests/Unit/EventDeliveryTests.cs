namespace OBSStudioClient.Tests.Unit
{
    using OBSStudioClient.Tests.TestSupport;

    /// <summary>
    /// Which events the client hands to the application. obs-websocket calls four of them high-volume, but only the volume meters
    /// are a stream of samples that may be thinned out: the others each describe a change of one input or one scene item, and
    /// the client dropped all but one of them every 100 ms (found by sending the events of a real OBS Studio one after the other,
    /// see ObsStudioCaptureTests).
    /// </summary>
    public class EventDeliveryTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        [Fact]
        public async Task TheStateChangesOfManyInputs_ThatComeAtOnce_AreAllRaised()
        {
            await using var server = new FakeObsWebSocketServer();
            server.Start();
            using var client = await ConnectAsync(server);
            var active = new List<string>();
            var showing = new List<string>();
            client.InputActiveStateChanged += (_, e) => { lock (active) { active.Add(e.InputName); } };
            client.InputShowStateChanged += (_, e) => { lock (showing) { showing.Add(e.InputName); } };

            // A scene switch changes the state of every input of both scenes at the same moment.
            foreach (var name in new[] { "Camera", "Game", "Microphone", "Overlay" })
            {
                await server.SendEventAsync("InputActiveStateChanged", new { inputName = name, inputUuid = Guid.NewGuid(), videoActive = true }, eventIntent: 131072);
                await server.SendEventAsync("InputShowStateChanged", new { inputName = name, inputUuid = Guid.NewGuid(), videoShowing = true }, eventIntent: 262144);
            }

            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => { lock (active) { lock (showing) { return active.Count == 4 && showing.Count == 4; } } }, Timeout), "Not every state change was raised.");
            Assert.Equal(new[] { "Camera", "Game", "Microphone", "Overlay" }, active);
            Assert.Equal(new[] { "Camera", "Game", "Microphone", "Overlay" }, showing);
        }

        [Fact]
        public async Task TheTransformsOfManySceneItems_ThatComeAtOnce_AreAllRaised()
        {
            await using var server = new FakeObsWebSocketServer();
            server.Start();
            using var client = await ConnectAsync(server);
            var items = new List<int>();
            client.SceneItemTransformChanged += (_, e) => { lock (items) { items.Add(e.SceneItemId); } };
            var sceneUuid = Guid.NewGuid();

            foreach (var id in new[] { 1, 2, 3 })
            {
                await server.SendEventAsync(
                    "SceneItemTransformChanged",
                    new { sceneName = "Scene", sceneUuid, sceneItemId = id, sceneItemTransform = new { positionX = id * 10.0, positionY = 0.0 } },
                    eventIntent: 524288);
            }

            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => { lock (items) { return items.Count == 3; } }, Timeout), "Not every transform was raised.");
            Assert.Equal(new[] { 1, 2, 3 }, items);
        }

        [Fact]
        public async Task TheVolumeMeters_AreThinnedOut_ButTheNextOneAfterAPauseIsRaised()
        {
            await using var server = new FakeObsWebSocketServer();
            server.Start();
            using var client = await ConnectAsync(server);
            var raised = 0;
            client.InputVolumeMeters += (_, _) => Interlocked.Increment(ref raised);

            // The volume meters are sent many times a second: the client passes on one every 100 ms.
            const int burst = 25;
            for (var count = 0; count < burst; count++)
            {
                await server.SendEventAsync("InputVolumeMeters", new { inputs = Array.Empty<object>() }, eventIntent: 65536);
            }

            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => Volatile.Read(ref raised) >= 1, Timeout), "No volume meters were raised.");
            await Task.Delay(300);
            var afterTheBurst = Volatile.Read(ref raised);
            Assert.InRange(afterTheBurst, 1, burst - 1);

            await server.SendEventAsync("InputVolumeMeters", new { inputs = Array.Empty<object>() }, eventIntent: 65536);

            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => Volatile.Read(ref raised) > afterTheBurst, Timeout), "The volume meters after a pause were not raised.");
        }

        private static async Task<ObsClient> ConnectAsync(FakeObsWebSocketServer server)
        {
            var client = new ObsClient { RequestTimeout = 5000 };
            var connect = client.ConnectAsync(hostname: "127.0.0.1", port: server.Port);
            await server.HandshakeAsync().WithTimeoutAsync(Timeout);
            await connect.WithTimeoutAsync(Timeout);
            return client;
        }
    }
}
