namespace OBSStudioClient.Tests.Model
{
    using OBSStudioClient.Model;
    using OBSStudioClient.Tests.Simulation;
    using OBSStudioClient.Tests.TestSupport;

    /// <summary>
    /// The levels of the audio (<see cref="ObsHighVolume.AudioLevels"/>), over the whole stack: an <see cref="ObsModel"/> on a real <see cref="ObsClient"/>, connected to the simulation of OBS Studio
    /// (<see cref="SimObsServer"/>), with the lags and the order of what OBS Studio really does. The simulation sends the levels when a test makes a tick (it has no clock of its own), and what the model shows of every
    /// input is what the last tick said (the inputs that are on the air and have audio, with their numbers).
    /// </summary>
    public class ModelAudioLevelsOverSimulationTests
    {
        private const string Audio = "wasapi_input_capture";
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

        // The program scene shows a microphone and a guitar (audio) and a picture (no audio); "Other" shows a second microphone. The model is connected first, as in the other tests of the changes: OBS Studio lists what was made a
        // moment ago late (Real and Hostile).
        private static async Task<ModelSimRig> StartAsync(SimQuirks? quirks = null, ObsHighVolume follow = ObsHighVolume.AudioLevels, bool autoReconnect = false, int seed = 7)
        {
            var rig = new ModelSimRig(quirks, new ObsModelOptions { HighVolume = follow, AutoReconnect = autoReconnect }, seed);
            try
            {
                await rig.ConnectAsync().WithTimeoutAsync(Patience);
                await rig.ConvergedAsync("the first read", Patience);
                var program = rig.Model.ProgramScene!.Uuid;
                rig.World(world =>
                {
                    world.CreateInput(program, "Mic", Audio);
                    world.CreateInput(program, "Guitar", Audio);
                    world.CreateInput(program, "Picture", Colour);
                    var other = world.CreateScene("Other");
                    world.CreateInput(other, "Spare", Audio);
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

        // What differs between what the last tick said and what the inputs of the model show, or null.
        private static string? Differences(ModelSimRig rig)
        {
            var truth = rig.Server.Run(sim => sim.LastMeters.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
            var problems = new List<string>();
            foreach (var input in rig.Model.Inputs)
            {
                truth.TryGetValue(input.Uuid.ToString("D"), out var expected);
                var actual = input.Levels;
                if (expected is null)
                {
                    if (actual is not null)
                    {
                        problems.Add($"{input.Name} has levels, and the last tick did not list it.");
                    }

                    continue;
                }

                if (actual is null)
                {
                    problems.Add($"{input.Name} has no levels, and the last tick listed it.");
                    continue;
                }

                if (actual.Channels.Count != expected.Length)
                {
                    problems.Add($"{input.Name} has {actual.Channels.Count} channels, and the last tick said {expected.Length}.");
                    continue;
                }

                for (var channel = 0; channel < expected.Length; channel++)
                {
                    var said = expected[channel];
                    var shown = actual.Channels[channel];
                    if (shown.Magnitude != said[0] || shown.Peak != said[1] || shown.InputPeak != said[2])
                    {
                        problems.Add($"{input.Name}, channel {channel}: the model shows {shown.Magnitude}/{shown.Peak}/{shown.InputPeak}, and the last tick said {said[0]}/{said[1]}/{said[2]}.");
                    }
                }
            }

            return problems.Count == 0 ? null : string.Join(Environment.NewLine, problems);
        }

        private static async Task LevelsConvergedAsync(ModelSimRig rig, string what)
        {
            var until = DateTime.UtcNow + Patience;
            string? differences;
            while ((differences = Differences(rig)) is not null)
            {
                if (DateTime.UtcNow > until)
                {
                    throw new TimeoutException($"The model did not show the levels of the last tick in time: {what}.{Environment.NewLine}{differences}");
                }

                await Task.Delay(10);
            }
        }

        // The messages of earlier ticks have been sent before the next one is made: with the order of what OBS Studio really does (Hostile may swap events of different changes) the last tick is the last that arrives
        // only when nothing is on its way.
        private static async Task QuietAsync(ModelSimRig rig, string what)
        {
            var until = DateTime.UtcNow + Patience;
            while (rig.Server.Run(sim => sim.NextDeadline) is not null)
            {
                if (DateTime.UtcNow > until)
                {
                    throw new TimeoutException($"The simulation did not go quiet in time: {what}.");
                }

                await Task.Delay(10);
            }
        }

        private static async Task TickAsync(ModelSimRig rig, string what)
        {
            await QuietAsync(rig, what);
            rig.World(world => world.MeterTick());
            await LevelsConvergedAsync(rig, what);
        }

        // The transition of a switch takes a while with the lags of OBS Studio, and the old scene is only off the air when it has ended.
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
        public async Task TheLevels_AreWhatTheSimulationSent_ForTheInputsThatAreOnTheAirAndHaveAudio(string preset)
        {
            await using var rig = await StartAsync(Quirks(preset));
            var mic = rig.Model.Inputs["Mic"];
            var guitar = rig.Model.Inputs["Guitar"];
            Assert.Null(mic.Levels);

            // The first message that lists an input has no channels yet; the next one has two.
            await TickAsync(rig, "the first tick");
            Assert.NotNull(mic.Levels);
            Assert.Empty(mic.Levels!.Channels);
            await TickAsync(rig, "the second tick");

            Assert.Equal(2, mic.Levels!.Channels.Count);
            Assert.Equal(2, guitar.Levels!.Channels.Count);
            Assert.Null(rig.Model.Inputs["Picture"].Levels);
            Assert.Null(rig.Model.Inputs["Spare"].Levels);
            Assert.Same(mic, mic.Levels.Input);
        }

        [Theory]
        [InlineData("Ideal")]
        [InlineData("Real")]
        [InlineData("Hostile")]
        public async Task TheLevels_FollowWhatComesOnTheAirAndWhatGoesOffIt(string preset)
        {
            await using var rig = await StartAsync(Quirks(preset));
            var mic = rig.Model.Inputs["Mic"];
            var spare = rig.Model.Inputs["Spare"];
            var other = rig.Model.Scenes["Other"].Uuid;
            var raised = new List<ObsAudioLevelsEventArgs>();
            rig.Model.AudioLevels += (_, e) =>
            {
                lock (raised)
                {
                    raised.Add(e);
                }
            };
            await TickAsync(rig, "the inputs of the program scene");
            await TickAsync(rig, "the inputs of the program scene, again");

            // The program scene changes: the inputs of the other scene are on the air, and those of the old one are off it when the transition has ended.
            rig.World(world => world.SwitchProgramScene(other));
            await AfterTheTransitionAsync(rig, "the program scene to be Other");
            await TickAsync(rig, "the inputs of the other scene");

            Assert.Null(mic.Levels);
            Assert.NotNull(spare.Levels);
            lock (raised)
            {
                Assert.Contains(raised, args => args.Ended.Contains(mic));
                Assert.Equal([spare], raised[^1].Levels.Select(levels => levels.Input).ToArray());
            }

            // An input that is renamed is still the input: the levels follow its UUID.
            rig.World(world => world.RenameInput(spare.Uuid, "Renamed"));
            await rig.ConvergedAsync("the input to be renamed", Patience);
            await TickAsync(rig, "the renamed input");
            await TickAsync(rig, "the renamed input, again");
            Assert.Equal(2, spare.Levels!.Channels.Count);
            Assert.Equal("Renamed", spare.Name);
        }

        [Theory]
        [InlineData("Ideal")]
        [InlineData("Real")]
        [InlineData("Hostile")]
        public async Task TheLevels_Of_AnItemThatIsHidden_AndOfAnInputThatIsDestroyed_AreGone(string preset)
        {
            await using var rig = await StartAsync(Quirks(preset));
            var mic = rig.Model.Inputs["Mic"];
            var guitar = rig.Model.Inputs["Guitar"];
            await TickAsync(rig, "the inputs of the program scene");
            await TickAsync(rig, "the inputs of the program scene, again");
            var program = rig.Model.ProgramScene!;

            rig.World(world => world.SetItemEnabled(program.Uuid, program.Items["Mic"].Id, false));
            await rig.ConvergedAsync("the item of the microphone to be hidden", Patience);
            await TickAsync(rig, "the microphone to be off the air");
            Assert.Null(mic.Levels);
            Assert.NotNull(guitar.Levels);

            // The last scene item of an input goes: OBS Studio destroys the input.
            var item = program.Items["Guitar"].Id;
            rig.World(world => world.RemoveItem(program.Uuid, item));
            await rig.ConvergedAsync("the guitar to be destroyed", Patience);
            await TickAsync(rig, "after the guitar was destroyed");

            // The input is not among the inputs of the model any more, so the tick does not wait for it: its levels go when the message that does not list it arrives.
            await ModelRig.EventuallyAsync(() => guitar.Levels is null, "the levels of the destroyed input to go", Patience);
            Assert.True(guitar.IsRemoved);
        }

        [Theory]
        [InlineData("Ideal")]
        [InlineData("Real")]
        [InlineData("Hostile")]
        public async Task TheLevels_AndTheActivity_AgreeOnWhichInputsAreOnTheAir(string preset)
        {
            await using var rig = await StartAsync(Quirks(preset), ObsHighVolume.All);
            var other = rig.Model.Scenes["Other"].Uuid;
            await TickAsync(rig, "the first tick");
            await TickAsync(rig, "the second tick");

            AssertAgree(rig.Model);

            rig.World(world =>
            {
                world.SetStudioMode(true);
                world.SwitchPreviewScene(other);
            });
            await rig.ConvergedAsync("studio mode with Other as the preview scene", Patience);
            await TickAsync(rig, "studio mode");
            await TickAsync(rig, "studio mode, again");

            // The preview shows the inputs of Other, which are not on the air: they have no levels.
            Assert.True(rig.Model.Inputs["Spare"].IsShowing);
            Assert.Null(rig.Model.Inputs["Spare"].Levels);
            AssertAgree(rig.Model);
        }

        private static void AssertAgree(ObsModel model)
        {
            foreach (var input in model.Inputs)
            {
                Assert.Equal(input.HasAudio && input.IsActive == true, input.Levels is not null);
            }
        }

        [Theory]
        [InlineData("Ideal")]
        [InlineData("Real")]
        [InlineData("Hostile")]
        public async Task TheLevels_AreTakenWhenTheConnectionIsLost_AndComeBackWithTheNextMessage(string preset)
        {
            await using var rig = await StartAsync(Quirks(preset), autoReconnect: true);
            var mic = rig.Model.Inputs["Mic"];
            await TickAsync(rig, "the first tick");
            await TickAsync(rig, "the second tick");
            var identified = rig.Server.IdentifiedCount;

            rig.Server.AbortConnection();
            await ModelRig.EventuallyAsync(() => mic.Levels is null, "the levels to be taken from the input", Patience);
            await ModelRig.EventuallyAsync(() => rig.Server.IdentifiedCount > identified && rig.Model.State == ObsModelState.Synchronized, "the model to be connected again", Patience);
            await rig.ConvergedAsync("the model to be read again", Patience);
            Assert.Null(mic.Levels);

            await TickAsync(rig, "the first tick of the new connection");
            await TickAsync(rig, "the second tick of the new connection");
            Assert.Equal(2, mic.Levels!.Channels.Count);
        }

        [Theory]
        [InlineData("Ideal")]
        [InlineData("Real")]
        [InlineData("Hostile")]
        public async Task TheLevels_FollowASwitchOfTheSceneCollection(string preset)
        {
            await using var rig = await StartAsync(Quirks(preset));
            var oldMic = rig.Model.Inputs["Mic"];
            await TickAsync(rig, "the first tick");
            await TickAsync(rig, "the second tick");

            rig.World(world => world.CreateCollection("Second"));
            await ModelRig.EventuallyAsync(() => oldMic.IsRemoved, "the old collection to be left", Patience);
            await rig.ConvergedAsync("the new collection to be read", Patience);
            var program = rig.Model.ProgramScene!.Uuid;
            rig.World(world => world.CreateInput(program, "Voice", Audio));
            await rig.ConvergedAsync("the new input", Patience);
            await TickAsync(rig, "the first tick of the new collection");
            await TickAsync(rig, "the second tick of the new collection");

            Assert.Null(oldMic.Levels);
            Assert.Equal(2, rig.Model.Inputs["Voice"].Levels!.Channels.Count);
            Assert.Single(rig.Model.Inputs, input => input.Levels is not null);
        }

        [Fact]
        public async Task AModelThatDoesNotFollowTheLevels_IsNotSentThem_AndShowsNone()
        {
            await using var rig = await StartAsync(ModelSimRig.DefaultQuirks, ObsHighVolume.None);
            var subscription = rig.Server.EventSubscriptions!.Value;
            Assert.Equal(0, subscription & 65536);

            rig.World(world => world.MeterTick());
            await Task.Delay(300);

            Assert.All(rig.Model.Inputs, input => Assert.Null(input.Levels));
        }
    }
}
