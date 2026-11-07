namespace OBSStudioClient.Tests.Studio
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Studio;
    using OBSStudioClient.Tests.Simulation;
    using OBSStudioClient.Tests.TestSupport;

    /// <summary>
    /// The changes that make, rename and remove scenes, inputs and scene items by name, over the whole stack: an <see cref="ObsStudio"/> on a real <see cref="ObsClient"/>, connected to
    /// the simulation of OBS Studio (<see cref="SimObsServer"/>), with the lags, the order, and the refusals of what OBS Studio really does. When a call returns, the model shows the
    /// change, and what it shows is what the simulation holds (<see cref="StudioMirror"/>). What the line of changes does when OBS Studio fails in ways on purpose is tested by
    /// <see cref="StudioCreateTests"/> and <see cref="StudioRenameRemoveTests"/>.
    /// </summary>
    public class StudioMakeOverSimulationTests
    {
        private const string Mic = "wasapi_input_capture";
        private const string Colour = "color_source_v3";
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

        // Two scenes, "Gaming" with three inputs (a camera, a microphone, a backdrop), and an empty "Chatting", next to the scene that a collection starts with. The studio is connected
        // first, and what is made is followed by its events: OBS Studio lists a scene that was made a moment ago late (Real and Hostile), so a connection that is made right after it
        // would not see it.
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
        public async Task EverythingThatIsMadeRenamedAndRemoved_IsShownByTheModel_WhenTheCallReturns(string preset)
        {
            await using var rig = await StartAsync(Quirks(preset));
            var studio = rig.Studio;
            var gaming = studio.Scenes["Gaming"];

            var streaming = await studio.CreateSceneAsync("Streaming").WithTimeoutAsync(Patience);
            Assert.Same(streaming, studio.Scenes["Streaming"]);
            Assert.Equal(ObsObjectState.Live, streaming.State);
            await rig.ConvergedAsync("a scene that was made", Patience);

            // An input with audio is complete when the call returns, and one without is made hidden, with settings.
            var headset = await streaming.CreateInputAsync("Headset", Mic).WithTimeoutAsync(Patience);
            Assert.Same(headset, studio.Inputs["Headset"]);
            Assert.True(headset.HasAudio);
            Assert.False(headset.IsMuted);
            Assert.Same(headset, streaming.Items["Headset"].Source);
            var logo = await streaming.CreateInputAsync("Logo", Colour, new { width = 640, height = 360 }, enabled: false).WithTimeoutAsync(Patience);
            Assert.False(streaming.Items["Logo"].IsEnabled);
            await rig.ConvergedAsync("inputs that were made", Patience);

            // The same source in another scene, and a scene in a scene.
            var shown = await gaming.AddAsync(headset).WithTimeoutAsync(Patience);
            Assert.Same(headset, shown.Source);
            Assert.Same(shown, gaming.Items.ById(shown.Id));
            var nested = await gaming.AddAsync(streaming, enabled: false).WithTimeoutAsync(Patience);
            Assert.Same(streaming, nested.Source);
            Assert.False(nested.IsEnabled);
            await rig.ConvergedAsync("scene items that were made", Patience);

            await streaming.RenameAsync("Live").WithTimeoutAsync(Patience);
            Assert.Equal("Live", streaming.Name);
            Assert.Same(streaming, studio.Scenes["Live"]);
            await logo.RenameAsync("Banner").WithTimeoutAsync(Patience);
            Assert.Same(logo, studio.Inputs["Banner"]);
            await rig.ConvergedAsync("a scene and an input that were renamed", Patience);

            await headset.SetEnabledInAllScenesAsync(false).WithTimeoutAsync(Patience);
            Assert.All(headset.GetSceneItems(), item => Assert.False(item.IsEnabled));
            Assert.Equal(2, headset.GetSceneItems().Count);
            await rig.ConvergedAsync("an input that was hidden everywhere", Patience);
            await headset.SetEnabledInAllScenesAsync(true).WithTimeoutAsync(Patience);
            Assert.All(headset.GetSceneItems(), item => Assert.True(item.IsEnabled));
            await rig.ConvergedAsync("an input that was shown everywhere", Patience);

            // An input that only one scene shows is destroyed with its last scene item, and one that two scenes show stays until the second goes.
            await logo.RemoveAsync().WithTimeoutAsync(Patience);
            Assert.True(logo.IsRemoved);
            Assert.False(studio.Inputs.Contains("Banner"));
            await rig.ConvergedAsync("an input that was removed", Patience);
            Assert.Equal(ObsObjectState.Gone, logo.State);
            await headset.RemoveAsync().WithTimeoutAsync(Patience);
            await rig.ConvergedAsync("an input that two scenes showed, removed", Patience);
            Assert.Equal(ObsObjectState.Gone, headset.State);
            Assert.False(gaming.Items.TryGet("Headset", out _));

            // A scene that another scene shows is removed with the scene item that shows it.
            await streaming.RemoveAsync().WithTimeoutAsync(Patience);
            Assert.True(streaming.IsRemoved);
            Assert.False(studio.Scenes.Contains("Live"));
            await rig.ConvergedAsync("a scene that was removed with the scene item that showed it", Patience);
            Assert.Equal(ObsObjectState.Gone, streaming.State);
            Assert.False(gaming.Items.TryGet("Live", out _));
        }

        [Theory]
        [InlineData("Ideal")]
        [InlineData("Real")]
        [InlineData("Hostile")]
        public async Task ScenesThatAreMadeTogether_AreMadeInTheOrderInWhichTheyWereAskedFor(string preset)
        {
            await using var rig = await StartAsync(Quirks(preset));
            var studio = rig.Studio;

            var first = studio.CreateSceneAsync("One");
            var second = studio.CreateSceneAsync("Two");
            var third = studio.CreateSceneAsync("Three");
            var made = await Task.WhenAll(first, second, third).WithTimeoutAsync(Patience);

            Assert.Equal(["One", "Two", "Three"], made.Select(scene => scene.Name).ToArray());
            await rig.ConvergedAsync("scenes that were made together", Patience);

            // OBS Studio lists the scene that was made last first: it did what it was asked in that order.
            Assert.Equal(["Three", "Two", "One"], studio.Scenes.Take(3).Select(scene => scene.Name).ToArray());
        }

        [Theory]
        [InlineData("Ideal")]
        [InlineData("Real")]
        [InlineData("Hostile")]
        public async Task ANameThatIsTaken_IsANameInUseException_ThatSaysWhatHoldsIt_AndNothingIsChanged(string preset)
        {
            await using var rig = await StartAsync(Quirks(preset));
            var studio = rig.Studio;
            var gaming = studio.Scenes["Gaming"];
            var chatting = studio.Scenes["Chatting"];
            var webcam = studio.Inputs["Webcam"];

            var scene = await Assert.ThrowsAsync<ObsNameInUseException>(() => studio.CreateSceneAsync("Gaming").WithTimeoutAsync(Patience));
            Assert.Same(gaming, scene.HeldBy);
            Assert.Contains("a scene named 'Gaming' exists already", scene.Message);

            var input = await Assert.ThrowsAsync<ObsNameInUseException>(() => chatting.CreateInputAsync("Webcam", Colour).WithTimeoutAsync(Patience));
            Assert.Same(webcam, input.HeldBy);
            var inputNamedLikeAScene = await Assert.ThrowsAsync<ObsNameInUseException>(() => chatting.CreateInputAsync("Gaming", Colour).WithTimeoutAsync(Patience));
            Assert.Same(gaming, inputNamedLikeAScene.HeldBy);
            Assert.Contains("an input can not have the name of a scene", inputNamedLikeAScene.Message);

            var renamedScene = await Assert.ThrowsAsync<ObsNameInUseException>(() => chatting.RenameAsync("Gaming").WithTimeoutAsync(Patience));
            Assert.Same(gaming, renamedScene.HeldBy);
            var ownName = await Assert.ThrowsAsync<ObsNameInUseException>(() => gaming.RenameAsync("Gaming").WithTimeoutAsync(Patience));
            Assert.Same(gaming, ownName.HeldBy);
            Assert.Contains("the scene already has the name 'Gaming'", ownName.Message);
            var renamedInput = await Assert.ThrowsAsync<ObsNameInUseException>(() => webcam.RenameAsync("Mic").WithTimeoutAsync(Patience));
            Assert.Same(studio.Inputs["Mic"], renamedInput.HeldBy);
            var inputRenamedLikeAScene = await Assert.ThrowsAsync<ObsNameInUseException>(() => webcam.RenameAsync("Chatting").WithTimeoutAsync(Patience));
            Assert.Same(chatting, inputRenamedLikeAScene.HeldBy);

            Assert.Equal("Gaming", gaming.Name);
            Assert.Equal("Webcam", webcam.Name);
            await rig.ConvergedAsync("names that were refused", Patience);
        }

        [Theory]
        [InlineData("Ideal")]
        [InlineData("Real")]
        [InlineData("Hostile")]
        public async Task ASceneThatIsNamedLikeAnInput_IsMade_ByOBSStudioThatAllowsIt(string preset)
        {
            await using var rig = await StartAsync(Quirks(preset));

            // obs-websocket 5.7 and newer: only the scenes of the canvas count for the name of a scene (an older one refuses it, and the exception says so).
            var scene = await rig.Studio.CreateSceneAsync("Webcam").WithTimeoutAsync(Patience);

            Assert.Same(scene, rig.Studio.Scenes["Webcam"]);
            Assert.NotNull(rig.Studio.Inputs["Webcam"]);
            await rig.ConvergedAsync("a scene named like an input", Patience);
        }

        [Theory]
        [InlineData("Ideal")]
        [InlineData("Real")]
        [InlineData("Hostile")]
        public async Task AnInputThatWasRemovedAndIsAlive_HoldsItsName_UntilItsSceneItemsAreRemoved(string preset)
        {
            await using var rig = await StartAsync(Quirks(preset));
            var studio = rig.Studio;
            var chatting = studio.Scenes["Chatting"];
            var webcam = studio.Inputs["Webcam"];

            // The user removes the input in OBS Studio, and the scene item of "Gaming" keeps it alive.
            rig.World(world => world.RemoveInput(webcam.Uuid));
            await StudioRig.EventuallyAsync(() => webcam.State == ObsObjectState.Detached, "the input to be detached", Patience);
            await rig.ConvergedAsync("an input that was removed and is alive", Patience);

            var exception = await Assert.ThrowsAsync<ObsNameInUseException>(() => chatting.CreateInputAsync("Webcam", Colour).WithTimeoutAsync(Patience));

            Assert.Same(webcam, exception.HeldBy);
            Assert.Contains("an input named 'Webcam' was removed, but OBS Studio keeps it alive because 1 scene item still shows it (scene item 1 of scene 'Gaming')", exception.Message);

            // What the exception says: removing it for good frees the name.
            await ((ObsInput)exception.HeldBy!).RemoveAsync().WithTimeoutAsync(Patience);
            await rig.ConvergedAsync("an input that was removed for good", Patience);
            Assert.Equal(ObsObjectState.Gone, webcam.State);
            var again = await chatting.CreateInputAsync("Webcam", Colour).WithTimeoutAsync(Patience);
            Assert.NotEqual(webcam.Uuid, again.Uuid);
            await rig.ConvergedAsync("an input made under the name that was freed", Patience);
        }

        [Theory]
        [InlineData("Ideal")]
        [InlineData("Real")]
        [InlineData("Hostile")]
        public async Task ASceneThatWasRemovedAndIsAlive_CanNotBeChanged_AndIsRemovedForGood_WithTheSceneItemThatKeepsItAlive(string preset)
        {
            await using var rig = await StartAsync(Quirks(preset));
            var studio = rig.Studio;
            var gaming = studio.Scenes["Gaming"];
            var chatting = studio.Scenes["Chatting"];
            await gaming.AddAsync(chatting).WithTimeoutAsync(Patience);
            rig.World(world => world.RemoveScene(chatting.Uuid));
            await StudioRig.EventuallyAsync(() => chatting.State == ObsObjectState.Detached, "the scene to be detached", Patience);
            await rig.ConvergedAsync("a scene that was removed and is alive", Patience);

            // OBS Studio never answers a rename of a scene that was removed (Hostile): the studio does not ask.
            var renamed = await Assert.ThrowsAsync<ObsRemovedException>(() => chatting.RenameAsync("Talking").WithTimeoutAsync(Patience));
            var added = await Assert.ThrowsAsync<ObsRemovedException>(() => chatting.CreateInputAsync("Logo", Colour).WithTimeoutAsync(Patience));
            Assert.Equal(ObsObjectState.Detached, renamed.State);
            Assert.Equal(ObsObjectState.Detached, added.State);

            await chatting.RemoveAsync().WithTimeoutAsync(Patience);
            await rig.ConvergedAsync("a scene that was removed for good", Patience);
            Assert.Equal(ObsObjectState.Gone, chatting.State);
            Assert.False(gaming.Items.TryGet("Chatting", out _));
        }

        [Theory]
        [InlineData("Ideal")]
        [InlineData("Real")]
        [InlineData("Hostile")]
        public async Task TheLastSceneOfACollection_IsNotRemoved_AndTheProgramSceneThatIsRemoved_IsReplacedByOBSStudio(string preset)
        {
            await using var rig = await StartAsync(Quirks(preset));
            var studio = rig.Studio;
            var program = studio.ProgramScene!;
            await studio.Scenes["Chatting"].RemoveAsync().WithTimeoutAsync(Patience);
            await studio.Scenes["Gaming"].RemoveAsync().WithTimeoutAsync(Patience);
            await rig.ConvergedAsync("scenes that were removed", Patience);
            Assert.Single(studio.Scenes);

            var refused = await Assert.ThrowsAsync<ObsResponseException>(() => program.RemoveAsync().WithTimeoutAsync(Patience));

            Assert.Equal(RequestStatusCode.NotEnoughResources, refused.ErrorCode);
            Assert.Equal(ObsObjectState.Live, program.State);
            Assert.Same(program, studio.ProgramScene);

            // With another scene, the program scene can be removed, and OBS Studio puts the first scene of the list on the air.
            var other = await studio.CreateSceneAsync("Other").WithTimeoutAsync(Patience);
            await program.RemoveAsync().WithTimeoutAsync(Patience);
            await rig.ConvergedAsync("the program scene that was removed", Patience);
            Assert.True(program.IsRemoved);
            Assert.Same(other, studio.ProgramScene);
        }

        [Theory]
        [InlineData("Ideal")]
        [InlineData("Real")]
        [InlineData("Hostile")]
        public async Task ASceneThatIsRemoved_TakesTheInputsThatOnlyItShowed_WithIt(string preset)
        {
            await using var rig = await StartAsync(Quirks(preset));
            var studio = rig.Studio;
            var gaming = studio.Scenes["Gaming"];
            var webcam = studio.Inputs["Webcam"];
            var mic = studio.Inputs["Mic"];
            await studio.Scenes["Chatting"].AddAsync(mic).WithTimeoutAsync(Patience);

            await gaming.RemoveAsync().WithTimeoutAsync(Patience);

            // OBS Studio destroys the inputs that only the scene showed a moment after the scene; the model learns of it from the events.
            await rig.ConvergedAsync("a scene that was removed with what it showed", Patience);
            Assert.Equal(ObsObjectState.Gone, gaming.State);
            Assert.Equal(ObsObjectState.Gone, webcam.State);
            Assert.Equal(ObsObjectState.Live, mic.State);
            Assert.Single(mic.GetSceneItems());
        }

        [Fact]
        public async Task ChangesOfSeveralKinds_AreMadeInTheOrderInWhichTheyWereAskedFor_AndTheModelEndsWhereOBSStudioEnds()
        {
            await using var rig = await StartAsync(SimQuirks.Real);
            var studio = rig.Studio;
            var gaming = studio.Scenes["Gaming"];
            var chatting = studio.Scenes["Chatting"];

            var scene = studio.CreateSceneAsync("Backstage");
            var rename = chatting.RenameAsync("Talking");
            var input = chatting.CreateInputAsync("Logo", Colour);
            var hide = studio.Inputs["Mic"].SetEnabledInAllScenesAsync(false);
            var item = gaming.AddAsync(chatting);
            var remove = studio.Inputs["Backdrop"].RemoveAsync();
            await Task.WhenAll(scene, rename, input, hide, item, remove).WithTimeoutAsync(Patience);

            await rig.ConvergedAsync("changes of several kinds", Patience);
            Assert.Equal("Talking", chatting.Name);
            Assert.False(studio.Inputs.Contains("Backdrop"));
            Assert.Same(chatting, (await item).Source);
        }
    }
}
