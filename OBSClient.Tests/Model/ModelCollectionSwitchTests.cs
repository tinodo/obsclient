namespace OBSStudioClient.Tests.Model
{
    using OBSStudioClient.Model;
    using OBSStudioClient.Tests.Simulation;
    using OBSStudioClient.Tests.TestSupport;

    /// <summary>
    /// OBS Studio changes its scene collection: the whole stack, from the application to the connection (see <see cref="ModelOverSimulationTests"/>), with the user of OBS
    /// Studio switching to another collection or making a new one, and with the client doing the same by a request. While OBS Studio does it, it answers every request with
    /// 207 and sends no event; when it is done, the model of the model holds what the new collection holds, and nothing of the one before it. The engine is tested against
    /// this by random walks in virtual time (<see cref="ModelEngineSimulationTests"/>) and by <see cref="ModelEngineCollectionTests"/>.
    /// </summary>
    public class ModelCollectionSwitchTests
    {
        private const string Mic = "wasapi_input_capture";
        private const string Colour = "color_source_v3";
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

        [Fact]
        public async Task TheCollectionThatOBSStudioSwitchesTo_ReplacesTheOldOne_AndWhatTheApplicationHoldsIsGone()
        {
            await using var rig = new ModelSimRig();
            var resynchronized = 0;
            rig.Model.Resynchronized += (_, _) => Interlocked.Increment(ref resynchronized);
            rig.World(world =>
            {
                var gaming = world.CreateScene("Gaming");
                world.CreateInput(gaming, "Mic", Mic);
                world.CreateInput(gaming, "Backdrop", Colour);
            });
            var original = rig.Server.Run(sim => sim.CurrentCollection);
            await rig.ConnectAsync().WithTimeoutAsync(Timeout);
            await rig.ConvergedAsync("the first read");
            await ModelRig.EventuallyAsync(() => Volatile.Read(ref resynchronized) == 1, "Resynchronized for the first read");

            var gamingScene = rig.Model.Scenes["Gaming"];
            var micInput = rig.Model.Inputs["Mic"];
            var micItem = gamingScene.Items["Mic"];
            var raised = new List<string>();
            gamingScene.PropertyChanged += (_, e) =>
            {
                lock (raised)
                {
                    raised.Add(e.PropertyName ?? string.Empty);
                }
            };

            rig.World(world => world.CreateCollection("Podcast"));
            await rig.ConvergedAsync("the collection that the user made");
            await ModelRig.EventuallyAsync(() => Volatile.Read(ref resynchronized) == 2, "Resynchronized after the switch");

            Assert.Equal(["Scene"], rig.Model.Scenes.Select(scene => scene.Name).ToArray());
            Assert.Empty(rig.Model.Inputs);

            // What the application held is about the collection that was left, and says so. It is told of it before Resynchronized, which comes last.
            Assert.Equal(ObsObjectState.Gone, gamingScene.State);
            Assert.True(gamingScene.IsRemoved);
            Assert.Equal(ObsObjectState.Gone, micInput.State);
            Assert.True(micItem.IsRemoved);
            lock (raised)
            {
                Assert.Contains(nameof(ObsScene.State), raised);
                Assert.Contains(nameof(ObsScene.IsRemoved), raised);
            }

            // The first collection comes back: it is the same scenes in OBS Studio, and it is new objects to the application, which do not revive the ones that it held.
            rig.World(world => world.SwitchCollection(original));
            await rig.ConvergedAsync("the first collection again");
            await ModelRig.EventuallyAsync(() => Volatile.Read(ref resynchronized) == 3, "Resynchronized after the switch back");

            var again = rig.Model.Scenes["Gaming"];
            Assert.NotSame(gamingScene, again);
            Assert.Equal(gamingScene.Uuid, again.Uuid);
            Assert.Equal(ObsObjectState.Live, again.State);
            Assert.Equal(ObsObjectState.Gone, gamingScene.State);
            Assert.NotSame(micInput, rig.Model.Inputs["Mic"]);
            Assert.Same(rig.Model.Inputs["Mic"], again.Items["Mic"].Source);
        }

        [Fact]
        public async Task WhileOBSStudioSwitches_TheModelHoldsWhatItKnew_SaysThatItIsBeingRead_AndAsksNothing()
        {
            // A switch that takes five seconds: long enough to look at what the model does in the middle of it, also on a slow or busy machine (the model is looked at about
            // 650 ms after it knew that OBS Studio is switching; a switch of a second and a half was over by then now and then, on a runner with two cores).
            await using var rig = new ModelSimRig(ModelSimRig.DefaultQuirks with { CollectionSwitchTime = SimRange.Exactly(5000) });
            rig.World(world => world.CreateInput(world.CreateScene("Gaming"), "Mic", Mic));
            await rig.ConnectAsync().WithTimeoutAsync(Timeout);
            await rig.ConvergedAsync("the first read");
            Assert.False(rig.Model.IsStale);

            rig.World(world => world.CreateCollection("Podcast"));
            await ModelRig.EventuallyAsync(() => rig.Model.State == ObsModelState.Synchronizing, "the model to know that OBS Studio is switching");

            // The model is of the collection that is being left, and the application is told that it is not up to date.
            Assert.True(rig.Model.IsStale);
            Assert.True(rig.Model.Scenes.Contains("Gaming"));
            Assert.True(rig.Model.Inputs.Contains("Mic"));

            // Everything that it asked would be answered 207, so it asks nothing: the requests that it sent before it knew are over by now.
            await Task.Delay(150);
            var requests = rig.Server.RequestCount;
            await Task.Delay(500);
            Assert.True(rig.Server.Run(sim => sim.IsSwitching), "The switch was over before the model was looked at: the test proves nothing.");
            Assert.Equal(requests, rig.Server.RequestCount);
            Assert.Equal(ObsModelState.Synchronizing, rig.Model.State);

            await rig.ConvergedAsync("the new collection", Patience);
            Assert.False(rig.Model.IsStale);
            Assert.Equal(["Scene"], rig.Model.Scenes.Select(scene => scene.Name).ToArray());
            Assert.Empty(rig.Model.Inputs);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(300)]
        public async Task ASceneCollectionThatTheClientSwitchesTo_IsFollowedByTheModel_AndWhatTheClientDoesAfterwardsIsDoneInTheNewOne(int switchMilliseconds)
        {
            await using var rig = new ModelSimRig(ModelSimRig.DefaultQuirks with { CollectionSwitchTime = SimRange.Exactly(switchMilliseconds) });
            rig.World(world => world.CreateInput(world.CreateScene("Gaming"), "Mic", Mic));
            var original = rig.Server.Run(sim => sim.CurrentCollection);
            await rig.ConnectAsync().WithTimeoutAsync(Timeout);
            await rig.ConvergedAsync("the first read");
            var client = rig.Model.Client;

            // A request that switches the scene collection is answered when OBS Studio is done, which takes longer than the 500 milliseconds that the client waits by
            // default: it would throw a TimeoutException, and OBS Studio would go on with the switch all the same.
            client.RequestTimeout = 15_000;

            // The request is answered when OBS Studio is done.
            await client.CreateSceneCollection("Podcast").WaitAsync(Patience);
            await rig.ConvergedAsync("the collection that the client made", Patience);
            Assert.Equal(["Scene"], rig.Model.Scenes.Select(scene => scene.Name).ToArray());

            await client.CreateScene("Interview");
            await client.CreateInput("Interview", "Camera", Colour);
            await rig.ConvergedAsync("what the client did in the new collection", Patience);
            Assert.True(rig.Model.Scenes.Contains("Interview"));
            Assert.True(rig.Model.Inputs.Contains("Camera"));

            await client.SetCurrentSceneCollection(original).WaitAsync(Patience);
            await rig.ConvergedAsync("the first collection again", Patience);
            Assert.True(rig.Model.Scenes.Contains("Gaming"));
            Assert.True(rig.Model.Inputs.Contains("Mic"));
            Assert.False(rig.Model.Scenes.Contains("Interview"));
            Assert.False(rig.Model.Inputs.Contains("Camera"));
        }

        [Fact]
        public async Task ConnectingWhileOBSStudioSwitches_WaitsForTheSwitch_AndShowsTheNewCollection()
        {
            await using var rig = new ModelSimRig(ModelSimRig.DefaultQuirks with { CollectionSwitchTime = SimRange.Exactly(1000) });
            rig.World(world => world.CreateInput(world.CreateScene("Gaming"), "Mic", Mic));
            rig.World(world => world.CreateCollection("Podcast"));
            Assert.True(rig.Server.Run(sim => sim.IsSwitching));

            // Everything that the model asks is answered 207 until OBS Studio is done, and what it asks then is about the new collection: the first of the events that it
            // gets is the end of the switch, which it did not see begin.
            await rig.ConnectAsync().WithTimeoutAsync(Patience);

            Assert.Null(rig.Compare());
            Assert.Equal(["Scene"], rig.Model.Scenes.Select(scene => scene.Name).ToArray());
            Assert.Empty(rig.Model.Inputs);
        }

        [Fact]
        public async Task AConnectionThatIsLostWhileOBSStudioSwitches_IsRestored_AndTheModelShowsTheNewCollection()
        {
            await using var rig = new ModelSimRig(ModelSimRig.DefaultQuirks with { CollectionSwitchTime = SimRange.Exactly(800) });
            rig.World(world => world.CreateInput(world.CreateScene("Gaming"), "Mic", Mic));
            await rig.ConnectAsync().WithTimeoutAsync(Timeout);
            await rig.ConvergedAsync("the first read");
            var gamingScene = rig.Model.Scenes["Gaming"];

            rig.World(world => world.CreateCollection("Podcast"));
            await ModelRig.EventuallyAsync(() => rig.Model.State == ObsModelState.Synchronizing, "the model to know that OBS Studio is switching");
            rig.Server.AbortConnection();

            // The client connects again by itself. The end of the switch may be sent to nobody: what the model reads then is the new collection all the same.
            await ModelRig.EventuallyAsync(() => rig.Model.State == ObsModelState.Synchronized && !rig.Server.Run(sim => sim.IsSwitching), "the model to be synchronized again", Patience);
            await rig.ConvergedAsync("the new collection, after the connection was restored", Patience);
            Assert.Equal(["Scene"], rig.Model.Scenes.Select(scene => scene.Name).ToArray());
            Assert.Equal(ObsObjectState.Gone, gamingScene.State);
        }

        [Fact]
        public async Task AConnectionThatIsLostInTheMiddleOfASwitch_IsRestoredWithNewObjects_WhenTheFirstCollectionIsLoadedAgain()
        {
            await using var rig = new ModelSimRig(ModelSimRig.DefaultQuirks with { CollectionSwitchTime = SimRange.Exactly(800) });
            rig.World(world => world.CreateInput(world.CreateScene("Gaming"), "Mic", Mic));
            var original = rig.Server.Run(sim => sim.CurrentCollection);
            await rig.ConnectAsync().WithTimeoutAsync(Timeout);
            await rig.ConvergedAsync("the first read");
            var gamingScene = rig.Model.Scenes["Gaming"];
            var micInput = rig.Model.Inputs["Mic"];

            // Nothing connects again by itself. The model sees a switch begin and loses the connection; OBS Studio finishes it, and goes back to the collection that it began with,
            // so its sources have the UUIDs that they had: the model cannot tell that it was away and back from the collection that it left never having changed.
            rig.Model.Client.AutoReconnect = false;
            rig.World(world => world.CreateCollection("Podcast"));
            await ModelRig.EventuallyAsync(() => rig.Model.State == ObsModelState.Synchronizing, "the model to know that OBS Studio is switching");
            rig.Server.AbortConnection();
            await ModelRig.EventuallyAsync(() => rig.Model.State == ObsModelState.Disconnected, "the state to follow the connection", Patience);
            await ModelRig.EventuallyAsync(() => !rig.Server.Run(sim => sim.IsSwitching), "OBS Studio to be done switching", Patience);
            rig.World(world => world.SwitchCollection(original));
            await ModelRig.EventuallyAsync(() => !rig.Server.Run(sim => sim.IsSwitching), "OBS Studio to be done switching back", Patience);

            await rig.ConnectAsync().WithTimeoutAsync(Timeout);
            await rig.ConvergedAsync("the first collection, after the connection was made again");

            // It is the same scene in OBS Studio, and a new object to the application: what it held belongs to the collection that was left.
            Assert.Equal(ObsObjectState.Gone, gamingScene.State);
            Assert.Equal(ObsObjectState.Gone, micInput.State);
            var again = rig.Model.Scenes["Gaming"];
            Assert.NotSame(gamingScene, again);
            Assert.Equal(gamingScene.Uuid, again.Uuid);
            Assert.NotSame(micInput, rig.Model.Inputs["Mic"]);
        }

        [Theory]
        [InlineData("Real")]
        [InlineData("Hostile")]
        public async Task TheModelFollowsOBSStudio_WhenItSwitchesItsSceneCollection_WithTheLagsAndTheOrderThatWereMeasured(string preset)
        {
            // The switch takes as long as it takes in OBS Studio (Hostile: up to four seconds, with the objects of the new collection announced while it loads), the
            // events and the answers lag, and the model has to end up with what OBS Studio holds, whatever order it all arrives in.
            await using var rig = new ModelSimRig(preset == "Real" ? SimQuirks.Real : SimQuirks.Hostile, seed: 7);
            var original = rig.Server.Run(sim => sim.CurrentCollection);
            var first = rig.Server.Run(sim => sim.Snapshot().Scenes[0].Uuid);
            await rig.ConnectAsync().WithTimeoutAsync(Patience);
            await rig.ConvergedAsync("the first read", Patience);

            async Task SwitchedAsync()
            {
                await ModelRig.EventuallyAsync(() => !rig.Server.Run(sim => sim.IsSwitching), "OBS Studio to be done switching", Patience);
            }

            var gaming = rig.World(world => world.CreateScene("Gaming"));
            var (mic, _) = rig.World(world => world.CreateInput(gaming, "Mic", Mic));
            rig.World(world => world.CreateInput(gaming, "Backdrop", Colour));
            rig.World(world => world.AddItem(first, mic));
            await rig.ConvergedAsync("a scene with two inputs, one of them shown by two scenes", Patience);

            rig.World(world => world.CreateCollection("Podcast"));
            await SwitchedAsync();
            await rig.ConvergedAsync("a new collection", Patience);
            Assert.Equal(["Scene"], rig.Model.Scenes.Select(scene => scene.Name).ToArray());

            var interview = rig.World(world => world.CreateScene("Interview"));
            rig.World(world => world.CreateInput(interview, "Camera", Colour));
            await rig.ConvergedAsync("what was made in the new collection", Patience);

            rig.World(world => world.SwitchCollection(original));
            await SwitchedAsync();
            await rig.ConvergedAsync("the first collection again", Patience);
            Assert.True(rig.Model.Scenes.Contains("Gaming"));
            Assert.False(rig.Model.Scenes.Contains("Interview"));

            // Several switches one after the other: the model ends up with the last collection, whatever came in between.
            rig.World(world => world.SwitchCollection("Podcast"));
            await SwitchedAsync();
            rig.World(world => world.SwitchCollection(original));
            await SwitchedAsync();
            rig.World(world => world.SwitchCollection("Podcast"));
            await SwitchedAsync();
            await rig.ConvergedAsync("the second collection, after several switches", Patience);
            Assert.True(rig.Model.Scenes.Contains("Interview"));
            Assert.True(rig.Model.Inputs.Contains("Camera"));
            Assert.False(rig.Model.Scenes.Contains("Gaming"));
        }
    }
}
