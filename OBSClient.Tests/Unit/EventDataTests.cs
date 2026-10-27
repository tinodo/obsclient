namespace OBSStudioClient.Tests.Unit
{
    using System.Text.Json;
    using OBSStudioClient.Events;
    using OBSStudioClient.Tests.TestSupport;

    /// <summary>
    /// The data of events that obs-websocket documents differently from what it sends. They were found by reading the events
    /// of a real OBS Studio (see ObsStudioCaptureTests), and checked in the source of obs-websocket 5.7.6.
    /// </summary>
    public class EventDataTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        [Fact]
        public async Task ACustomEvent_GivesTheDataThatTheOtherClientBroadcast()
        {
            await using var server = new FakeObsWebSocketServer();
            server.Start();
            using var client = await ConnectAsync(server);
            var awaiter = new EventAwaiter<CustomEventEventArgs>();
            client.CustomEvent += awaiter.Handler;

            // BroadcastCustomEvent makes obs-websocket send the data as the data of the event itself, not as a member of it.
            await server.SendEventAsync("CustomEvent", new { greeting = "hello", numbers = new[] { 1, 2 }, nested = new { ok = true } });

            var args = await awaiter.Task.WithTimeoutAsync(Timeout, "CustomEvent handler was not invoked.");
            Assert.True(args.EventData.HasValue, "The data of the custom event did not arrive.");
            var data = args.EventData!.Value;
            Assert.Equal("hello", data.GetProperty("greeting").GetString());
            Assert.Equal(2, data.GetProperty("numbers").GetArrayLength());
            Assert.True(data.GetProperty("nested").GetProperty("ok").GetBoolean());
        }

        [Fact]
        public void TheDataOfACustomEvent_IsWrittenAsItWasRead()
        {
            var args = JsonSerializer.Deserialize<CustomEventEventArgs>("""{"greeting":"hello","numbers":[1,2]}""")!;

            Assert.Equal("""{"greeting":"hello","numbers":[1,2]}""", JsonSerializer.Serialize(args));
        }

        [Fact]
        public async Task TheVolumeMeters_GiveTheNameTheUuidAndTheLevelsOfEveryInput()
        {
            await using var server = new FakeObsWebSocketServer();
            server.Start();
            using var client = await ConnectAsync(server);
            var awaiter = new EventAwaiter<InputVolumeMetersEventArgs>();
            client.InputVolumeMeters += awaiter.Handler;
            var uuid = Guid.NewGuid();

            await server.SendEventAsync(
                "InputVolumeMeters",
                new { inputs = new[] { new { inputName = "Mic/Aux", inputUuid = uuid, inputLevelsMul = new[] { new[] { 0.5f, 0.25f, 0.75f } } } } },
                eventIntent: 65536);

            var args = await awaiter.Task.WithTimeoutAsync(Timeout, "InputVolumeMeters handler was not invoked.");
            var meter = Assert.Single(args.Inputs);
            Assert.Equal("Mic/Aux", meter.InputName);
            Assert.Equal(uuid, meter.InputUuid);
            Assert.Equal(0.5f, meter.InputLevelsMul[0][0]);
            Assert.Null(meter.InputKind);
            Assert.Null(meter.UnversionedInputKind);
        }

        [Fact]
        public async Task AReindexedScene_GivesOnlyTheIdAndTheIndexOfItsItems()
        {
            await using var server = new FakeObsWebSocketServer();
            server.Start();
            using var client = await ConnectAsync(server);
            var awaiter = new EventAwaiter<SceneItemListReindexedEventArgs>();
            client.SceneItemListReindexed += awaiter.Handler;

            await server.SendEventAsync(
                "SceneItemListReindexed",
                new
                {
                    sceneName = "Scene",
                    sceneUuid = Guid.NewGuid(),
                    sceneItems = new[] { new { sceneItemId = 2, sceneItemIndex = 0 }, new { sceneItemId = 1, sceneItemIndex = 1 } },
                },
                eventIntent: 128);

            var args = await awaiter.Task.WithTimeoutAsync(Timeout, "SceneItemListReindexed handler was not invoked.");
            Assert.Equal(2, args.SceneItems.Length);
            Assert.Equal(2, args.SceneItems[0].SceneItemId);
            Assert.Equal(0, args.SceneItems[0].SceneItemIndex);
            Assert.Equal(1, args.SceneItems[1].SceneItemId);
            Assert.Equal(1, args.SceneItems[1].SceneItemIndex);
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
