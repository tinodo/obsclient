namespace OBSStudioClient.Tests.Studio
{
    using OBSStudioClient.Studio;
    using OBSStudioClient.Tests.TestSupport;

    /// <summary>
    /// What the host of the model does when OBS Studio changes its scene collection, with a stand-in for OBS Studio that says what a test tells it to say, when it is told to
    /// (<see cref="StudioCollectionSwitchTests"/> is the whole stack over the simulation, and <see cref="StudioEngineCollectionTests"/> is the engine).
    /// </summary>
    public class StudioHostCollectionTests
    {
        private const int ConfigEvents = 2;

        [Fact]
        public async Task AReadThatIsUnderWayWhenTheSceneCollectionChanges_DoesNotHoldUpTheReadsOfTheNewOne()
        {
            // A read may take 30 seconds, and the one that is under way is never answered: if the host waited for it, the new collection would be shown after that.
            await using var rig = await StudioRig.ConnectedAsync(world => world.AddScene("Gaming"), new ObsStudioOptions { ReadTimeout = TimeSpan.FromSeconds(30) });
            rig.Responder.AnswerBatches = false;
            var created = Guid.NewGuid();
            rig.Responder.Change(world =>
            {
                world.Scenes.Insert(0, (created, "New"));
                world.Items[created] = [];
            });
            var batches = rig.Responder.Batches;
            await rig.Responder.SendEventAsync("SceneCreated", new { sceneName = "New", sceneUuid = created, isGroup = false }, 4);
            await StudioRig.EventuallyAsync(() => rig.Responder.Batches > batches, "the read of the new scene to be sent");

            // OBS Studio switches. The collection that it loads has one scene, and what is asked from now on is answered about it.
            rig.Responder.Change(SwitchToPodcast);
            await ChangingAsync(rig, "Untitled");
            await StudioRig.EventuallyAsync(() => rig.Studio.State == ObsStudioState.Synchronizing, "the studio to know that OBS Studio is switching");
            rig.Responder.AnswerBatches = true;
            await ChangedAsync(rig, "Podcast");

            await StudioRig.EventuallyAsync(
                () => rig.Studio.State == ObsStudioState.Synchronized && rig.Studio.Scenes.Contains("Podcast"),
                "the studio to show the new collection, without waiting for the read that was lost",
                TimeSpan.FromSeconds(5));
            Assert.Equal(["Podcast"], rig.Studio.Scenes.Select(scene => scene.Name).ToArray());
        }

        [Fact]
        public async Task ASwitchWhileConnectAsyncIsStillReading_EndsWithTheNewCollection()
        {
            // The connection is made in the middle of a switch: the first read is never answered, and the first the studio hears of the switch is its end.
            await using var rig = StudioRig.Create(world => world.AddScene("Gaming"), new ObsStudioOptions { ReadTimeout = TimeSpan.FromSeconds(30) });
            rig.Responder.AnswerBatches = false;
            var connecting = rig.ConnectAsync();
            await StudioRig.EventuallyAsync(() => rig.Responder.Batches >= 1, "the first read to be sent");

            rig.Responder.Change(SwitchToPodcast);
            rig.Responder.AnswerBatches = true;
            await ChangedAsync(rig, "Podcast");

            await connecting.WithTimeoutAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(ObsStudioState.Synchronized, rig.Studio.State);
            Assert.Equal(["Podcast"], rig.Studio.Scenes.Select(scene => scene.Name).ToArray());
        }

        private static void SwitchToPodcast(FakeWorld world)
        {
            world.Scenes.Clear();
            world.Items.Clear();
            world.Inputs.Clear();
            world.Program = null;
            world.AddScene("Podcast");
        }

        private static Task ChangingAsync(StudioRig rig, string left)
        {
            return rig.Responder.SendEventAsync("CurrentSceneCollectionChanging", new { sceneCollectionName = left }, ConfigEvents);
        }

        private static Task ChangedAsync(StudioRig rig, string now)
        {
            return rig.Responder.SendEventAsync("CurrentSceneCollectionChanged", new { sceneCollectionName = now }, ConfigEvents);
        }
    }
}
