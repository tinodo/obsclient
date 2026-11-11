namespace OBSStudioClient.Tests.Model
{
    using OBSStudioClient.Model;
    using OBSStudioClient.Tests.Simulation;
    using OBSStudioClient.Tests.TestSupport;

    /// <summary>
    /// Whether the inputs are on the air and shown (<see cref="ObsHighVolume.Activity"/>), over the whole stack: an <see cref="ObsModel"/> on a real <see cref="ObsClient"/>, connected to the simulation of OBS
    /// Studio (<see cref="SimObsServer"/>), with the lags and the order of what OBS Studio really does. Whatever someone else does (the program scene, nested scenes, items that are hidden, studio mode) is
    /// followed, and what the model shows is what the simulation holds (<see cref="ModelMirror"/>, which compares the activity when the model follows it).
    /// </summary>
    public class ModelActivityOverSimulationTests
    {
        private const string Colour = "color_source_v3";
        private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

        private static SimQuirks Quirks(string preset)
        {
            return preset switch
            {
                "Ideal" => ModelSimRig.DefaultQuirks,
                "Real" => SimQuirks.Real,
                "Hostile" => SimQuirks.Hostile,
                _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, null),
            };
        }

        // The program scene shows the "Overlay"; "Gaming" shows a webcam and a backdrop; "Chatting" shows a nest of its own and is not the program scene. The model is connected first, as in the other tests of the
        // changes: OBS Studio lists what was made a moment ago late (Real and Hostile).
        private static async Task<ModelSimRig> StartAsync(SimQuirks? quirks = null, ObsHighVolume follow = ObsHighVolume.Activity, int seed = 7)
        {
            var rig = new ModelSimRig(quirks, new ObsModelOptions { HighVolume = follow }, seed);
            try
            {
                await rig.ConnectAsync().WithTimeoutAsync(Patience);
                await rig.ConvergedAsync("the first read", Patience);
                var program = rig.Model.ProgramScene!.Uuid;
                rig.World(world =>
                {
                    world.CreateInput(program, "Overlay", Colour);
                    var gaming = world.CreateScene("Gaming");
                    world.CreateInput(gaming, "Webcam", Colour);
                    world.CreateInput(gaming, "Backdrop", Colour);
                    var chatting = world.CreateScene("Chatting");
                    world.CreateInput(chatting, "Chat", Colour);
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

        // The transition of a switch takes a while with the lags of OBS Studio, and what is only so when it has ended (the old scene is off the air) is not there before: the model equals OBS Studio at every moment
        // of it, so the wait is for the transition.
        private static async Task AfterTheTransitionAsync(ModelSimRig rig, string what)
        {
            var until = DateTime.UtcNow + Patience;
            while (rig.Server.Run(sim => sim.Snapshot().TransitionRunning))
            {
                if (DateTime.UtcNow > until)
                {
                    throw new TimeoutException($"The transition did not end in time: {what}.");
                }

                await Task.Delay(10);
            }

            await rig.ConvergedAsync(what, Patience);
        }

        [Theory]
        [InlineData("Ideal")]
        [InlineData("Real")]
        [InlineData("Hostile")]
        public async Task TheActivity_OfTheInputs_FollowsTheProgramScene_AndWhatShowsTheInputs(string preset)
        {
            await using var rig = await StartAsync(Quirks(preset));
            var overlay = rig.Model.Inputs["Overlay"];
            var webcam = rig.Model.Inputs["Webcam"];
            var chat = rig.Model.Inputs["Chat"];

            // What is on the air at the start: the program scene, and nothing else.
            Assert.True(overlay.IsActive);
            Assert.True(overlay.IsShowing);
            Assert.False(webcam.IsActive);
            Assert.False(webcam.IsShowing);

            // The program scene changes: the inputs of the scene that goes on the air are on it, and those of the scene that was are off it when the transition has ended.
            var gaming = rig.Model.Scenes["Gaming"].Uuid;
            rig.World(world => world.SwitchProgramScene(gaming));
            await AfterTheTransitionAsync(rig, "the program scene to be Gaming");
            Assert.True(webcam.IsActive);
            Assert.False(overlay.IsActive);
            Assert.False(overlay.IsShowing);

            // A scene that is nested in the program scene puts its inputs on the air, as long as the item that shows it is enabled.
            var chatting = rig.Model.Scenes["Chatting"].Uuid;
            var nesting = rig.World(world => world.AddItem(gaming, chatting));
            await rig.ConvergedAsync("a scene to be nested in the program scene", Patience);
            Assert.True(chat.IsActive);

            rig.World(world => world.SetItemEnabled(gaming, nesting, false));
            await rig.ConvergedAsync("the nesting item to be hidden", Patience);
            Assert.False(chat.IsActive);
            Assert.False(chat.IsShowing);

            // An item of the program scene that is hidden takes its input off the air.
            var item = rig.Model.Scenes["Gaming"].Items["Webcam"].Id;
            rig.World(world => world.SetItemEnabled(gaming, item, false));
            await rig.ConvergedAsync("the webcam to be hidden", Patience);
            Assert.False(webcam.IsActive);
            Assert.False(webcam.IsShowing);
            Assert.Contains(rig.Changes, change => change.Kind == ObsChangeKind.PropertyChanged && change.PropertyName == nameof(ObsInput.IsActive) && ReferenceEquals(change.Entity, webcam));
        }

        [Theory]
        [InlineData("Ideal")]
        [InlineData("Real")]
        [InlineData("Hostile")]
        public async Task TheActivity_InStudioMode_ShowsThePreviewButDoesNotPutItOnTheAir(string preset)
        {
            await using var rig = await StartAsync(Quirks(preset));
            var webcam = rig.Model.Inputs["Webcam"];
            var overlay = rig.Model.Inputs["Overlay"];
            var gaming = rig.Model.Scenes["Gaming"].Uuid;

            rig.World(world =>
            {
                world.SetStudioMode(true);
                world.SwitchPreviewScene(gaming);
            });
            await rig.ConvergedAsync("studio mode with Gaming as the preview scene", Patience);

            Assert.False(webcam.IsActive);
            Assert.True(webcam.IsShowing);
            Assert.True(overlay.IsActive);

            // The transition of studio mode puts the preview on the air, and the old program scene becomes the preview.
            rig.World(world => world.TriggerTransition());
            await AfterTheTransitionAsync(rig, "the transition of studio mode");

            Assert.True(webcam.IsActive);
            Assert.False(overlay.IsActive);
            Assert.True(overlay.IsShowing);

            // Studio mode off: what only the preview showed is not shown any more.
            rig.World(world => world.SetStudioMode(false));
            await rig.ConvergedAsync("studio mode to be off", Patience);

            Assert.False(overlay.IsShowing);
            Assert.True(webcam.IsActive);
        }

        [Theory]
        [InlineData("Ideal")]
        [InlineData("Real")]
        [InlineData("Hostile")]
        public async Task AnInputThatOBSStudioMakes_HasItsActivity_WhenTheModelTellsOfIt(string preset)
        {
            await using var rig = await StartAsync(Quirks(preset));
            var told = new List<ObsInput>();
            rig.Model.Changed += (_, e) =>
            {
                if (e.Kind == ObsChangeKind.Added && e.Entity is ObsInput input)
                {
                    lock (told)
                    {
                        told.Add(input);
                    }
                }
            };

            var program = rig.Model.ProgramScene!.Uuid;
            rig.World(world => world.CreateInput(program, "Logo", Colour));
            await rig.ConvergedAsync("an input that is new", Patience);

            ObsInput[] added;
            lock (told)
            {
                added = told.ToArray();
            }

            var logo = Assert.Single(added);
            Assert.True(logo.IsActive);
            Assert.True(logo.IsShowing);
        }

        [Fact]
        public async Task WhenTheModelDoesNotFollowTheActivity_NoInputHasOne()
        {
            await using var rig = await StartAsync(follow: ObsHighVolume.None);
            var gaming = rig.Model.Scenes["Gaming"].Uuid;

            rig.World(world => world.SwitchProgramScene(gaming));
            await rig.ConvergedAsync("the program scene to be Gaming", Patience);

            Assert.All(rig.Model.Inputs, input =>
            {
                Assert.Null(input.IsActive);
                Assert.Null(input.IsShowing);
            });
        }
    }
}
