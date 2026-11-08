namespace OBSStudioClient.Tests.Model
{
    using System.Diagnostics;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Model;
    using OBSStudioClient.Tests.Simulation;
    using OBSStudioClient.Tests.TestSupport;
    using Xunit.Abstractions;

    /// <summary>
    /// The whole stack, from the application to the connection: an <see cref="ObsModel"/> on a real <see cref="ObsClient"/>, connected to the simulation of OBS Studio
    /// (<see cref="SimObsServer"/>), which is changed as the user of OBS Studio changes it, and by what the client asks for. After every change the model has to show
    /// what the simulation holds (<see cref="ModelMirror"/>). The engine of the model is tested against the simulation by random walks in virtual time
    /// (<see cref="ModelEngineSimulationTests"/>); these tests are what is left that those cannot see: the connection, the host, the objects that an application
    /// uses, and the time that it takes.
    /// </summary>
    public class ModelOverSimulationTests(ITestOutputHelper output)
    {
        private const string Mic = "wasapi_input_capture";
        private const string Colour = "color_source_v3";
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        [Fact]
        public async Task ConnectAsync_ReadsEverythingThatOBSStudioHolds_BeforeItReturns()
        {
            await using var rig = new ModelSimRig();
            rig.World(world =>
            {
                var gaming = world.CreateScene("Gaming");
                var chatting = world.CreateScene("Chatting");
                var (mic, _) = world.CreateInput(gaming, "Mic", Mic);
                world.CreateInput(gaming, "Backdrop", Colour);
                world.AddItem(chatting, mic);
                world.CreateGlobalInput("Desktop Audio", "wasapi_output_capture");
            });

            await rig.ConnectAsync().WithTimeoutAsync(Timeout);

            // Nothing is waited for after the call: the model is complete when it returns.
            Assert.Equal(ObsModelState.Synchronized, rig.Model.State);
            Assert.Null(rig.Compare());
            Assert.Equal(["Chatting", "Gaming", "Scene"], rig.Model.Scenes.Select(scene => scene.Name).ToArray());
            Assert.Equal("Scene", rig.Model.ProgramScene?.Name);
        }

        [Fact]
        public async Task ScenesInputsAndSceneItems_AreFoundByName()
        {
            await using var rig = new ModelSimRig();
            rig.World(world =>
            {
                var gaming = world.CreateScene("Gaming");
                var chatting = world.CreateScene("Chatting");
                var (mic, _) = world.CreateInput(gaming, "Mic", Mic);
                world.CreateInput(gaming, "Backdrop", Colour);
                world.AddItem(chatting, mic);
            });
            await rig.ConnectAsync().WithTimeoutAsync(Timeout);
            var model = rig.Model;

            var gamingScene = model.Scenes["Gaming"];
            var micInput = model.Inputs["Mic"];

            // The one input is the source of the scene item of each of the scenes that show it: no name is looked up twice, and no ID is needed.
            Assert.Same(micInput, gamingScene.Items["Mic"].Source);
            Assert.Same(micInput, model.Scenes["Chatting"].Items["Mic"].Source);
            Assert.True(micInput.HasAudio);
            Assert.False(model.Inputs["Backdrop"].HasAudio);
            Assert.Equal(2, gamingScene.Items.Count);
            Assert.False(model.Scenes.Contains("Nothing"));
            Assert.Throws<ObsNotFoundException>(() => model.Scenes["Nothing"]);
        }

        [Fact]
        public async Task EveryChangeThatTheUserMakesInOBSStudio_IsFollowedByTheModel()
        {
            await using var rig = new ModelSimRig();
            var original = rig.Server.Run(sim => sim.Snapshot().Scenes[0].Uuid);
            await rig.ConnectAsync().WithTimeoutAsync(Timeout);
            await rig.ConvergedAsync("the first read");

            async Task StepAsync(string what, Action<SimExternal> change)
            {
                rig.World(change);
                await rig.ConvergedAsync(what);
            }

            var gaming = rig.World(world => world.CreateScene("Gaming"));
            await rig.ConvergedAsync("a new scene");
            Assert.True(rig.Model.Scenes.Contains("Gaming"));

            await StepAsync("a renamed scene", world => world.RenameScene(gaming, "Streaming"));
            Assert.True(rig.Model.Scenes.Contains("Streaming"));
            Assert.False(rig.Model.Scenes.Contains("Gaming"));

            var (mic, micItem) = rig.World(world => world.CreateInput(gaming, "Mic", Mic));
            await rig.ConvergedAsync("a new input with a scene item");
            var (_, backdropItem) = rig.World(world => world.CreateInput(gaming, "Backdrop", Colour));
            await rig.ConvergedAsync("a second input");
            var micObject = rig.Model.Inputs["Mic"];

            var shared = rig.World(world => world.AddItem(original, mic));
            await rig.ConvergedAsync("an input that two scenes show");
            Assert.Same(micObject, rig.Model.Scenes["Scene"].Items["Mic"].Source);

            await StepAsync("a scene item that moved", world => world.SetItemIndex(gaming, backdropItem, 0));
            Assert.Equal("Backdrop", rig.Model.Scenes["Streaming"].Items[0].Source.Name);
            await StepAsync("a scene item that was disabled", world => world.SetItemEnabled(gaming, micItem, false));
            await StepAsync("a scene item that was locked", world => world.SetItemLocked(gaming, micItem, true));
            await StepAsync("an input that was muted", world => world.SetMute(mic, true));
            Assert.True(micObject.IsMuted);
            await StepAsync("an input whose volume changed", world => world.SetVolumeDb(mic, -12));
            await StepAsync("a renamed input", world => world.RenameInput(mic, "Microphone"));
            Assert.Same(micObject, rig.Model.Inputs["Microphone"]);
            await StepAsync("another program scene", world => world.SwitchProgramScene(gaming));
            Assert.Equal("Streaming", rig.Model.ProgramScene?.Name);

            await StepAsync("a scene item that was removed", world => world.RemoveItem(gaming, backdropItem));
            Assert.Single(rig.Model.Scenes["Streaming"].Items);

            // OBS Studio keeps an input alive that is removed while scene items show it: the model hides nothing, and says what became of it.
            await StepAsync("an input that was removed while two scene items show it", world => world.RemoveInput(mic));
            Assert.Equal(ObsObjectState.Detached, micObject.State);
            Assert.False(rig.Model.Inputs.Contains("Microphone"));
            Assert.Same(micObject, rig.Model.Scenes["Streaming"].Items[0].Source);

            await StepAsync("the first of the scene items that kept it alive was removed", world => world.RemoveItem(gaming, micItem));
            Assert.Equal(ObsObjectState.Detached, micObject.State);
            await StepAsync("the last scene item that kept it alive was removed", world => world.RemoveItem(original, shared));

            // Nothing reaches the input any more, so the comparison cannot see it: it is waited for by itself. The model takes the scene item away a moment before it
            // marks the input as gone, and a read of the model in between sees the one without the other.
            await ModelRig.EventuallyAsync(() => micObject.State == ObsObjectState.Gone, "the input to be gone, now that no scene item shows it");

            await StepAsync("a scene that was removed", world => world.RemoveScene(gaming));
            Assert.False(rig.Model.Scenes.Contains("Streaming"));
            Assert.Equal(["Scene"], rig.Model.Scenes.Select(scene => scene.Name).ToArray());
        }

        [Fact]
        public async Task WhatTheClientAsksOBSStudioFor_IsFollowedByTheModel()
        {
            await using var rig = new ModelSimRig();
            await rig.ConnectAsync().WithTimeoutAsync(Timeout);
            await rig.ConvergedAsync("the first read");
            var client = rig.Model.Client;

            // Names do for the requests of the client already (a scene or an input is given by its name or its UUID): the model is what knows them.
            var created = await client.CreateScene("FromClient");
            await rig.ConvergedAsync("a scene that the client made");
            Assert.Equal(created, rig.Model.Scenes["FromClient"].Uuid);

            var camera = await client.CreateInput("FromClient", "Camera", Colour);
            var mic = await client.CreateInput("FromClient", "Mic", Mic);
            await rig.ConvergedAsync("two inputs that the client made");
            Assert.Equal(camera.InputUuid, rig.Model.Inputs["Camera"].Uuid);

            await client.SetInputMute("Mic", true);
            await client.SetSceneItemEnabled("FromClient", camera.SceneItemId, false);
            await client.SetSceneItemIndex("FromClient", mic.SceneItemId, 0);
            await client.SetCurrentProgramScene("FromClient");
            await rig.ConvergedAsync("what the client changed");
            Assert.True(rig.Model.Inputs["Mic"].IsMuted);
            Assert.Equal("FromClient", rig.Model.ProgramScene?.Name);

            await client.SetSceneName("FromClient", "Renamed");
            await client.RemoveInput("Camera");
            await rig.ConvergedAsync("a scene that was renamed and an input that was removed");
            Assert.True(rig.Model.Scenes.Contains("Renamed"));
            Assert.False(rig.Model.Inputs.Contains("Camera"));
        }

        [Fact]
        public async Task ASceneThatOBSStudioAnnouncesTwice_IsAddedOnce()
        {
            // The quirk is in the default quirks: OBS Studio sends SceneCreated twice (obs-websocket#1364).
            await using var rig = new ModelSimRig();
            await rig.ConnectAsync().WithTimeoutAsync(Timeout);
            await rig.ConvergedAsync("the first read");

            rig.World(world => world.CreateScene("Twice"));
            await rig.ConvergedAsync("the scene");

            // The notifications come from the notifier, which is not in step with the model: the one that tells of the scene is waited for, and then a moment for a
            // second one, which there must not be.
            await ModelRig.EventuallyAsync(() => rig.Changes.Any(change => change.Kind == ObsChangeKind.Added && change.Entity is ObsScene { Name: "Twice" }), "the notification of the new scene");
            await Task.Delay(200);

            Assert.Equal(1, rig.Changes.Count(change => change.Kind == ObsChangeKind.Added && change.Entity is ObsScene { Name: "Twice" }));
            Assert.Equal(1, rig.Model.Scenes.Count(scene => scene.Name == "Twice"));
            await rig.ConvergedAsync("the scene, after the second announcement");
        }

        [Fact]
        public async Task AConnectionThatWasLost_IsRestored_AndTheModelShowsWhatChangedMeanwhile()
        {
            await using var rig = new ModelSimRig();
            var original = rig.Server.Run(sim => sim.Snapshot().Scenes[0].Uuid);

            // Resynchronized is raised after the first read too, by the notifier, which is not in step with the call: it is counted from the start.
            var resynchronized = 0;
            rig.Model.Resynchronized += (_, _) => Interlocked.Increment(ref resynchronized);
            await rig.ConnectAsync().WithTimeoutAsync(Timeout);
            await rig.ConvergedAsync("the first read");
            await ModelRig.EventuallyAsync(() => Volatile.Read(ref resynchronized) == 1, "Resynchronized for the first read");

            rig.Server.AbortConnection();
            await ModelRig.EventuallyAsync(() => rig.Model.State == ObsModelState.Reconnecting, "the state to follow the connection");
            Assert.True(rig.Model.IsStale);

            // OBS Studio changes while nobody is connected to it: nothing is sent to anybody.
            rig.World(world =>
            {
                var gaming = world.CreateScene("Gaming");
                world.CreateInput(gaming, "Mic", Mic);
                world.RenameScene(original, "Main");
            });

            // The client connects again by itself, and the model reads everything again.
            await ModelRig.EventuallyAsync(() => rig.Model.State == ObsModelState.Synchronized && Volatile.Read(ref resynchronized) == 2, "the model to be synchronized again", TimeSpan.FromSeconds(30));
            await rig.ConvergedAsync("what changed while the connection was lost");
            Assert.False(rig.Model.IsStale);
            Assert.True(rig.Model.Scenes.Contains("Main"));
            Assert.True(rig.Model.Inputs.Contains("Mic"));
        }

        [Theory]
        [InlineData("Real")]
        [InlineData("Hostile")]
        public async Task TheModelFollowsOBSStudio_WithTheLagsAndTheOrderThatWereMeasured(string preset)
        {
            // The events and the answers of the simulation come after the times that were measured in OBS Studio (and the list of scenes lags up to seconds), in the
            // order that they come in there, and the model has to end up with what OBS Studio holds, whatever order it all arrives in.
            await using var rig = new ModelSimRig(preset == "Real" ? SimQuirks.Real : SimQuirks.Hostile, seed: 7);
            var original = rig.Server.Run(sim => sim.Snapshot().Scenes[0].Uuid);
            await rig.ConnectAsync().WithTimeoutAsync(TimeSpan.FromSeconds(30));
            await rig.ConvergedAsync("the first read", TimeSpan.FromSeconds(30));
            var patience = TimeSpan.FromSeconds(30);

            var gaming = rig.World(world => world.CreateScene("Gaming"));
            var (mic, micItem) = rig.World(world => world.CreateInput(gaming, "Mic", Mic));
            var (backdrop, _) = rig.World(world => world.CreateInput(gaming, "Backdrop", Colour));
            var shared = rig.World(world => world.AddItem(original, mic));
            rig.World(world => world.SetMute(mic, true));
            rig.World(world => world.RenameScene(gaming, "Streaming"));
            await rig.ConvergedAsync("a scene with two inputs, one of them shown by two scenes", patience);

            rig.World(world => world.RemoveInput(mic));
            rig.World(world => world.SetItemEnabled(gaming, micItem, false));
            await rig.ConvergedAsync("an input that was removed while two scene items show it", patience);

            rig.World(world => world.RemoveItem(gaming, micItem));
            rig.World(world => world.RemoveItem(original, shared));
            rig.World(world => world.RemoveInput(backdrop));
            await rig.ConvergedAsync("the last scene items of the removed inputs were removed", patience);

            rig.World(world => world.RemoveScene(gaming));
            await rig.ConvergedAsync("a scene that was removed", patience);
            Assert.Equal(["Scene"], rig.Model.Scenes.Select(scene => scene.Name).ToArray());
            Assert.Empty(rig.Model.Inputs);
        }

        [Fact]
        public async Task AWorldOfAHundredScenesAndAThousandSceneItems_IsReadInTime()
        {
            const int sceneCount = 100;
            const int itemsPerScene = 10;
            await using var rig = new ModelSimRig();
            rig.World(world =>
            {
                var scenes = Enumerable.Range(0, sceneCount).Select(i => world.CreateScene($"Scene {i:000}")).ToArray();

                // Every scene has an input of its own (half of them with audio) and shows nine that other scenes have.
                var inputs = new Guid[sceneCount];
                for (var i = 0; i < sceneCount; i++)
                {
                    inputs[i] = world.CreateInput(scenes[i], $"Input {i:000}", i % 2 == 0 ? Mic : Colour).InputUuid;
                }

                for (var i = 0; i < sceneCount; i++)
                {
                    for (var k = 1; k < itemsPerScene; k++)
                    {
                        world.AddItem(scenes[i], inputs[(i + k) % sceneCount]);
                    }
                }
            });

            var watch = Stopwatch.StartNew();
            await rig.ConnectAsync().WithTimeoutAsync(TimeSpan.FromSeconds(30));
            watch.Stop();

            var items = rig.Model.Scenes.Sum(scene => scene.Items.Count);
            var withAudio = rig.Model.Inputs.Count(input => input.HasAudio);
            output.WriteLine($"ConnectAsync read {rig.Model.Scenes.Count} scenes, {items} scene items and {rig.Model.Inputs.Count} inputs ({withAudio} with audio) in {watch.Elapsed.TotalMilliseconds:N0} ms, over the loopback address, from a simulation without latency.");
            Assert.Equal(sceneCount + 1, rig.Model.Scenes.Count);
            Assert.Equal(sceneCount * itemsPerScene, items);
            Assert.Equal(sceneCount / 2, withAudio);
            Assert.Null(rig.Compare());

            // The limit is generous: what is measured is written to the output of the test, and a slow machine must not fail the test of a model that is right.
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(15), $"ConnectAsync took {watch.Elapsed.TotalSeconds:N1} seconds.");
        }
    }
}
