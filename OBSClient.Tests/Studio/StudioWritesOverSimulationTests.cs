namespace OBSStudioClient.Tests.Studio
{
    using System.Diagnostics;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Studio;
    using OBSStudioClient.Tests.Simulation;
    using OBSStudioClient.Tests.TestSupport;

    /// <summary>
    /// The changes that the application asks for, over the whole stack: an <see cref="ObsStudio"/> on a real <see cref="ObsClient"/>, connected to the simulation of OBS Studio
    /// (<see cref="SimObsServer"/>), with the lags and the order of what OBS Studio really does. When a call returns, the model shows the change, and what it shows is what
    /// the simulation holds (<see cref="StudioMirror"/>). The line of changes is tested against a stand-in that fails in ways on purpose by <see cref="StudioWriterTests"/>.
    /// </summary>
    public class StudioWritesOverSimulationTests
    {
        private const string Mic = "wasapi_input_capture";
        private const string Colour = "color_source_v3";
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

        private static SimQuirks Quirks(string preset)
        {
            return preset switch
            {
                "Ideal" => StudioSimRig.DefaultQuirks,
                "Real" => SimQuirks.Real,
                "Hostile" => SimQuirks.Hostile,
                _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, null),
            };
        }

        // Two scenes, "Gaming" with three inputs (a camera, a microphone, a backdrop), and an empty "Chatting". The studio is connected first, and what is made is
        // followed by its events: OBS Studio lists a scene that was made a moment ago late (Real and Hostile), so a connection that is made right after it would not see it.
        private static async Task<StudioSimRig> StartAsync(SimQuirks? quirks = null, ObsStudioOptions? options = null, int seed = 7)
        {
            var rig = new StudioSimRig(quirks, options, seed);
            try
            {
                await rig.ConnectAsync().WithTimeoutAsync(Patience);
                await rig.ConvergedAsync("the first read", Patience);
                rig.World(world =>
                {
                    var gaming = world.CreateScene("Gaming");
                    world.CreateInput(gaming, "Webcam", Colour);
                    world.CreateInput(gaming, "Mic", Mic);
                    world.CreateInput(gaming, "Backdrop", Colour);
                    world.CreateScene("Chatting");
                });
                await rig.ConvergedAsync("the scenes and the inputs", Patience);
                return rig;
            }
            catch
            {
                await rig.DisposeAsync();
                throw;
            }
        }

        [Theory]
        [InlineData("Ideal")]
        [InlineData("Real")]
        [InlineData("Hostile")]
        public async Task EveryChangeOfASceneItem_IsShownByTheModel_WhenTheCallReturns(string preset)
        {
            await using var rig = await StartAsync(Quirks(preset));
            var gaming = rig.Studio.Scenes["Gaming"];
            var webcam = gaming.Items["Webcam"];

            await webcam.SetEnabledAsync(false).WithTimeoutAsync(Patience);
            Assert.False(webcam.IsEnabled);
            await rig.ConvergedAsync("a scene item that was hidden", Patience);

            await webcam.SetEnabledAsync(true).WithTimeoutAsync(Patience);
            Assert.True(webcam.IsEnabled);

            await webcam.SetLockedAsync(true).WithTimeoutAsync(Patience);
            Assert.True(webcam.IsLocked);
            await webcam.SetLockedAsync(false).WithTimeoutAsync(Patience);
            Assert.False(webcam.IsLocked);

            // The scene items between the old and the new place move by one, which OBS Studio says in one event for the scene.
            await webcam.SetIndexAsync(2).WithTimeoutAsync(Patience);
            Assert.Equal(2, webcam.Index);
            await rig.ConvergedAsync("a scene item that was moved to the top", Patience);
            await webcam.SetIndexAsync(0).WithTimeoutAsync(Patience);
            Assert.Equal(0, webcam.Index);
            await rig.ConvergedAsync("a scene item that was moved to the bottom", Patience);

            Assert.False(await webcam.ToggleEnabledAsync().WithTimeoutAsync(Patience));
            Assert.False(webcam.IsEnabled);
            Assert.True(await webcam.ToggleEnabledAsync().WithTimeoutAsync(Patience));
            Assert.True(webcam.IsEnabled);

            await webcam.RemoveAsync().WithTimeoutAsync(Patience);
            Assert.True(webcam.IsRemoved);
            await rig.ConvergedAsync("a scene item that was removed (with its input, which nothing else showed)", Patience);
            Assert.Equal(2, gaming.Items.Count);
        }

        [Theory]
        [InlineData("Ideal")]
        [InlineData("Real")]
        [InlineData("Hostile")]
        public async Task EveryChangeOfAnInput_IsShownByTheModel_WhenTheCallReturns(string preset)
        {
            await using var rig = await StartAsync(Quirks(preset));
            var mic = rig.Studio.Inputs["Mic"];

            await mic.SetMutedAsync(true).WithTimeoutAsync(Patience);
            Assert.True(mic.IsMuted);
            await mic.SetMutedAsync(false).WithTimeoutAsync(Patience);
            Assert.False(mic.IsMuted);

            Assert.True(await mic.ToggleMuteAsync().WithTimeoutAsync(Patience));
            Assert.True(mic.IsMuted);
            Assert.False(await mic.ToggleMuteAsync().WithTimeoutAsync(Patience));
            Assert.False(mic.IsMuted);

            await mic.SetVolumeDbAsync(-12).WithTimeoutAsync(Patience);
            Assert.InRange(mic.VolumeDb, -12.01f, -11.99f);
            await rig.ConvergedAsync("an input whose volume was set in decibels", Patience);
            await mic.SetVolumeMulAsync(0.5).WithTimeoutAsync(Patience);
            Assert.InRange(mic.VolumeMul, 0.4999f, 0.5001f);
            await rig.ConvergedAsync("an input whose volume was set as a multiplier", Patience);
            await mic.SetVolumeDbAsync(-100).WithTimeoutAsync(Patience);
            Assert.InRange(mic.VolumeMul, 0f, 0.0001f);
            Assert.InRange(mic.VolumeDb, -100.01f, -99.99f);
            await mic.SetVolumeDbAsync(0).WithTimeoutAsync(Patience);
            Assert.InRange(mic.VolumeMul, 0.9999f, 1.0001f);
            await rig.ConvergedAsync("an input whose volume is back at 0 dB", Patience);
        }

        [Theory]
        [InlineData("Ideal")]
        [InlineData("Real")]
        [InlineData("Hostile")]
        public async Task ASceneThatIsSwitchedTo_IsTheProgramScene_WhenTheCallReturns(string preset)
        {
            await using var rig = await StartAsync(Quirks(preset));
            var gaming = rig.Studio.Scenes["Gaming"];
            var chatting = rig.Studio.Scenes["Chatting"];

            await chatting.SwitchToAsync().WithTimeoutAsync(Patience);
            Assert.True(chatting.IsProgram);
            Assert.Same(chatting, rig.Studio.ProgramScene);
            await gaming.SwitchToAsync().WithTimeoutAsync(Patience);
            Assert.True(gaming.IsProgram);
            Assert.False(chatting.IsProgram);

            // A scene that is on the air already: nothing to wait for.
            await gaming.SwitchToAsync().WithTimeoutAsync(Patience);
            Assert.True(gaming.IsProgram);
            await rig.ConvergedAsync("the program scene", Patience);
        }

        [Fact]
        public async Task ASwitchThroughATransitionThatTakesLongerThanTheWait_IsShownByAReadOfTheProgramScene()
        {
            // OBS Studio announces the new program scene when the transition has ended, two seconds from now, and answers a question about it at once.
            await using var rig = await StartAsync(StudioSimRig.DefaultQuirks with { TransitionDuration = TimeSpan.FromSeconds(2) });
            var chatting = rig.Studio.Scenes["Chatting"];
            var watch = Stopwatch.StartNew();

            await chatting.SwitchToAsync().WithTimeoutAsync(Patience);

            watch.Stop();
            Assert.True(chatting.IsProgram);
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(1.9), $"The call waited for the end of the transition: {watch.Elapsed.TotalSeconds:N1} seconds.");
            await rig.ConvergedAsync("the program scene after the transition", Patience);
        }

        [Theory]
        [InlineData("Ideal")]
        [InlineData("Real")]
        [InlineData("Hostile")]
        public async Task ChangesThatAreAskedForTogether_AreMadeInTheOrderInWhichTheyWereAskedFor(string preset)
        {
            // OBS Studio can answer requests that arrive together in any order (Real and Hostile do): the last change asked for is the one that holds.
            await using var rig = await StartAsync(Quirks(preset));
            var webcam = rig.Studio.Scenes["Gaming"].Items["Webcam"];
            var mic = rig.Studio.Inputs["Mic"];

            var calls = new List<Task>();
            for (var i = 0; i < 30; i++)
            {
                calls.Add(webcam.SetEnabledAsync(i % 2 == 0));
                calls.Add(mic.SetMutedAsync(i % 3 == 0));
                calls.Add(webcam.SetIndexAsync(i % 3));
            }

            await Task.WhenAll(calls).WithTimeoutAsync(Patience);

            Assert.False(webcam.IsEnabled);
            Assert.True(mic.IsMuted == (29 % 3 == 0));
            Assert.Equal(29 % 3, webcam.Index);
            await rig.ConvergedAsync("after thirty changes of each kind", Patience);
        }

        [Fact]
        public async Task TogglesThatAreAskedForTogether_MakeAsManyToggles()
        {
            await using var rig = await StartAsync(SimQuirks.Real);
            var webcam = rig.Studio.Scenes["Gaming"].Items["Webcam"];
            var mic = rig.Studio.Inputs["Mic"];

            var items = Enumerable.Range(0, 5).Select(_ => webcam.ToggleEnabledAsync()).ToArray();
            var inputs = Enumerable.Range(0, 5).Select(_ => mic.ToggleMuteAsync()).ToArray();
            var itemResults = await Task.WhenAll(items).WithTimeoutAsync(Patience);
            var inputResults = await Task.WhenAll(inputs).WithTimeoutAsync(Patience);

            // The scene item starts shown and the input unmuted: five toggles end with the scene item hidden and the input muted.
            Assert.Equal([false, true, false, true, false], itemResults);
            Assert.Equal([true, false, true, false, true], inputResults);
            Assert.False(webcam.IsEnabled);
            Assert.True(mic.IsMuted);
            await rig.ConvergedAsync("after five toggles each", Patience);
        }

        [Theory]
        [InlineData("Ideal")]
        [InlineData("Real")]
        [InlineData("Hostile")]
        public async Task TogglesAndChangesThatAreAskedForTogether_AreMadeInTheOrderInWhichTheyWereAskedFor(string preset)
        {
            await using var rig = await StartAsync(Quirks(preset));
            var webcam = rig.Studio.Scenes["Gaming"].Items["Webcam"];

            var toggle1 = webcam.ToggleEnabledAsync();
            var show = webcam.SetEnabledAsync(true);
            var toggle2 = webcam.ToggleEnabledAsync();
            var toggle3 = webcam.ToggleEnabledAsync();
            var hide = webcam.SetEnabledAsync(false);
            var toggle4 = webcam.ToggleEnabledAsync();
            await Task.WhenAll(toggle1, show, toggle2, toggle3, hide, toggle4).WithTimeoutAsync(Patience);

            // The scene item starts shown: the first toggle hides it, the change shows it, the next two toggles hide it and show it, the change hides it, the last toggle shows it.
            Assert.Equal(new[] { false, false, true, true }, new[] { await toggle1, await toggle2, await toggle3, await toggle4 });
            Assert.True(webcam.IsEnabled);
            await rig.ConvergedAsync("after toggles and changes of a scene item that were asked for together", Patience);
        }

        [Fact]
        public async Task AChange_ThatAnotherClientMakesAtTheSameTime_IsFollowedToo()
        {
            await using var rig = await StartAsync(SimQuirks.Real);
            var webcam = rig.Studio.Scenes["Gaming"].Items["Webcam"];
            var scene = rig.Studio.Scenes["Gaming"].Uuid;
            var micItem = rig.Studio.Scenes["Gaming"].Items["Mic"];

            var change = webcam.SetEnabledAsync(false);
            rig.World(world => world.SetItemEnabled(scene, micItem.Id, false));
            await change.WithTimeoutAsync(Patience);

            Assert.False(webcam.IsEnabled);
            await rig.ConvergedAsync("a change of the user next to a change of the application", Patience);
            Assert.False(micItem.IsEnabled);
        }

        [Fact]
        public async Task AChange_IsNotAsked_OfAnObjectThatWasRemoved_OrWithoutAConnection()
        {
            await using var rig = await StartAsync(options: new ObsStudioOptions { AutoReconnect = false });
            var gaming = rig.Studio.Scenes["Gaming"];
            var webcam = gaming.Items["Webcam"];
            var requests = rig.Server.RequestCount;
            rig.World(world => world.RemoveItem(gaming.Uuid, webcam.Id));
            await rig.ConvergedAsync("a scene item that the user removed", Patience);

            var removed = await Assert.ThrowsAsync<ObsRemovedException>(() => webcam.SetEnabledAsync(true).WithTimeoutAsync(Timeout));
            Assert.Equal(ObsObjectState.Gone, removed.State);

            await rig.Studio.DisconnectAsync().WithTimeoutAsync(Timeout);
            await Assert.ThrowsAsync<ObsNotConnectedException>(() => gaming.Items["Mic"].SetLockedAsync(true).WithTimeoutAsync(Timeout));
            Assert.Equal(requests, rig.Server.RequestCount);
        }

        [Fact]
        public async Task AChange_IsNotAsked_WhileOBSStudioChangesItsSceneCollection_AndWhatWasHeldIsGoneAfterwards()
        {
            await using var rig = await StartAsync(StudioSimRig.DefaultQuirks with { CollectionSwitchTime = SimRange.Exactly(1500) });
            var webcam = rig.Studio.Scenes["Gaming"].Items["Webcam"];
            var mic = rig.Studio.Inputs["Mic"];
            rig.World(world => world.CreateCollection("Podcast"));
            await StudioRig.EventuallyAsync(() => rig.Studio.State == ObsStudioState.Synchronizing, "the studio to know that OBS Studio is switching");
            var requests = rig.Server.RequestCount;

            await Assert.ThrowsAsync<ObsNotReadyException>(() => webcam.SetEnabledAsync(false).WithTimeoutAsync(Timeout));
            await Assert.ThrowsAsync<ObsNotReadyException>(() => mic.SetMutedAsync(true).WithTimeoutAsync(Timeout));
            Assert.Equal(requests, rig.Server.RequestCount);

            // When the switch is over, what the application holds is of the collection that was left.
            await rig.ConvergedAsync("the new collection", Patience);
            await rig.Studio.WaitUntilSettledAsync(Patience);
            requests = rig.Server.RequestCount;
            await Assert.ThrowsAsync<ObsRemovedException>(() => webcam.SetEnabledAsync(false).WithTimeoutAsync(Timeout));
            await Assert.ThrowsAsync<ObsRemovedException>(() => mic.SetMutedAsync(true).WithTimeoutAsync(Timeout));
            Assert.Equal(requests, rig.Server.RequestCount);
        }

        [Fact]
        public async Task AChangeThatWaitsForItsTurn_IsNotSent_WhenTheCallerGivesUp()
        {
            await using var rig = await StartAsync(SimQuirks.Hostile);
            var webcam = rig.Studio.Scenes["Gaming"].Items["Webcam"];

            // Every change takes some milliseconds in the simulation: the last one is given up long before its turn.
            var calls = Enumerable.Range(0, 19).Select(i => webcam.SetEnabledAsync(i % 2 == 1)).ToList();
            using var cancellation = new CancellationTokenSource();
            var given = webcam.SetEnabledAsync(true, cancellation.Token);
            await cancellation.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => given.WithTimeoutAsync(Timeout));
            await Task.WhenAll(calls).WithTimeoutAsync(Patience);

            // The 19th change (number 18) hides the scene item, and the one that was given up would have shown it.
            await rig.ConvergedAsync("after the changes", Patience);
            Assert.False(webcam.IsEnabled);
        }
    }
}
