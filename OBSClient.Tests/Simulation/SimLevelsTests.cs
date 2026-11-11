namespace OBSStudioClient.Tests.Simulation
{
    using System.Text.Json;

    /// <summary>
    /// The levels of the audio (<c>InputVolumeMeters</c>) in the simulation, as OBS Studio 33.0.0 sends them (measured by LiveLevelsTests): only when a tick is made, for the inputs that are on the air and have audio, in the
    /// order in which they came on the air, with the volume and the mute of the input applied to the first two numbers of a channel and not to the third, and without channels in the first message that lists an input.
    /// </summary>
    public class SimLevelsTests
    {
        private const string Audio = "wasapi_input_capture";
        private const string Colour = "color_source_v3";

        // The last message of the volume meters, after a tick: the name of each input that it lists, and its levels as the message says them.
        private static List<(string Name, float[][] Levels)> Tick(SimHarness h)
        {
            h.ClearLog();
            h.Sim.External.MeterTick();
            h.Settle();
            var message = Assert.Single(h.Events("InputVolumeMeters"));
            return [.. message.Data.GetProperty("inputs").EnumerateArray().Select(entry => (
                entry.GetProperty("inputName").GetString()!,
                entry.GetProperty("inputLevelsMul").EnumerateArray().Select(channel => channel.EnumerateArray().Select(number => number.GetSingle()).ToArray()).ToArray()))];
        }

        private static string[] Names(List<(string Name, float[][] Levels)> listed)
        {
            return [.. listed.Select(entry => entry.Name)];
        }

        [Fact]
        public void NothingIsSent_UntilATickIsMade_NotEvenWhenInputsAreOnTheAir()
        {
            var h = new SimHarness();
            h.CreateInput(h.InitialScene, "Mic", Audio);
            h.Settle();

            Assert.Empty(h.Events("InputVolumeMeters"));
            h.Run(5000);
            Assert.Empty(h.Events("InputVolumeMeters"));
        }

        [Fact]
        public void ATick_WithNothingOnTheAir_IsAMessageWithoutInputs()
        {
            var h = new SimHarness();

            Assert.Empty(Tick(h));
        }

        [Fact]
        public void OnlyTheInputsThatAreOnTheAirAndHaveAudioAreListed()
        {
            var h = new SimHarness();
            var other = h.CreateScene("Other");
            h.CreateInput(h.InitialScene, "Mic", Audio);
            h.CreateInput(h.InitialScene, "Picture", Colour);
            h.CreateInput(other, "Spare", Audio);
            h.Settle();

            Assert.Equal(["Mic"], Names(Tick(h)));

            h.Ok("SetCurrentProgramScene", new { sceneUuid = other.ToString() });
            h.Settle();

            Assert.Equal(["Spare"], Names(Tick(h)));
        }

        [Fact]
        public void TheFirstMessageThatListsAnInput_HasNoChannels_AndTheNextOnesHaveTwo()
        {
            var h = new SimHarness();
            h.CreateInput(h.InitialScene, "Mic", Audio);
            h.Settle();

            var first = Assert.Single(Tick(h));
            var second = Assert.Single(Tick(h));

            Assert.Empty(first.Levels);
            Assert.Equal(2, second.Levels.Length);
            Assert.All(second.Levels, channel => Assert.Equal(3, channel.Length));
            Assert.Equal(h.Sim.Snapshot().Inputs.Single().Name, second.Name);
        }

        [Fact]
        public void AMutedInput_IsListed_WithoutMagnitudeAndPeak_ButWithThePeakOfTheInput()
        {
            var h = new SimHarness();
            var (mic, _) = h.CreateInput(h.InitialScene, "Mic", Audio);
            h.Settle();
            Tick(h);
            h.Ok("SetInputMute", new { inputUuid = mic.ToString(), inputMuted = true });
            h.Settle();

            var muted = Assert.Single(Tick(h));

            Assert.All(muted.Levels, channel =>
            {
                Assert.Equal(0f, channel[0]);
                Assert.Equal(0f, channel[1]);
                Assert.True(channel[2] > 0f);
            });
        }

        [Fact]
        public void TheVolume_IsAppliedToTheFirstTwoNumbers_AndNotToThePeakOfTheInput()
        {
            var h = new SimHarness();
            var (mic, _) = h.CreateInput(h.InitialScene, "Mic", Audio);
            h.Settle();
            h.Ok("SetInputVolume", new { inputUuid = mic.ToString(), inputVolumeMul = 0.5 });
            h.Settle();
            Tick(h);

            var listed = Assert.Single(Tick(h));

            Assert.All(listed.Levels, channel =>
            {
                Assert.Equal(channel[2] * 0.5f, channel[1]);
                Assert.True(channel[0] <= channel[1]);
            });
        }

        [Fact]
        public void TheInputs_AreListedInTheOrderInWhichTheyCameOnTheAir()
        {
            var h = new SimHarness();
            var other = h.CreateScene("Other");
            var (b, _) = h.CreateInput(other, "B", Audio);
            h.CreateInput(h.InitialScene, "A", Audio);
            h.CreateInput(h.InitialScene, "C", Audio);
            h.Settle();
            Assert.Equal(["A", "C"], Names(Tick(h)));

            // B comes on the air after them (it is put in the program scene), and A goes off the air and comes back: it is the last then.
            h.CreateSceneItem(h.InitialScene, b);
            h.Settle();
            Assert.Equal(["A", "C", "B"], Names(Tick(h)));
            var item = h.Items(h.InitialScene).Single(entry => entry.Source == "A").Id;
            h.Ok("SetSceneItemEnabled", new { sceneUuid = h.InitialScene.ToString(), sceneItemId = item, sceneItemEnabled = false });
            h.Settle();
            Assert.Equal(["C", "B"], Names(Tick(h)));
            h.Ok("SetSceneItemEnabled", new { sceneUuid = h.InitialScene.ToString(), sceneItemId = item, sceneItemEnabled = true });
            h.Settle();

            Assert.Equal(["C", "B", "A"], Names(Tick(h)));
        }

        [Fact]
        public void AnInputThatIsDestroyed_IsNotListedAnyMore()
        {
            var h = new SimHarness();
            var (_, micItem) = h.CreateInput(h.InitialScene, "Mic", Audio);
            h.CreateInput(h.InitialScene, "Guitar", Audio);
            h.Settle();
            Assert.Equal(["Mic", "Guitar"], Names(Tick(h)));

            // The last scene item of an input goes: OBS Studio destroys the input, and says nothing of its activity.
            h.Ok("RemoveSceneItem", new { sceneUuid = h.InitialScene.ToString(), sceneItemId = micItem });
            h.Settle();

            Assert.Equal(["Guitar"], Names(Tick(h)));
        }

        [Fact]
        public void TheLastTick_IsWhatTheSimulationRemembers_ForTheTestsThatCompareItWithAModel()
        {
            var h = new SimHarness();
            h.CreateInput(h.InitialScene, "Mic", Audio);
            h.Settle();
            Tick(h);

            var listed = Assert.Single(Tick(h));
            var remembered = Assert.Single(h.Sim.LastMeters);

            Assert.Equal(listed.Levels.Length, remembered.Value.Length);
            Assert.Equal(listed.Levels[0], remembered.Value[0]);
        }

        [Fact]
        public void ATickIsAnEventOfTheHighVolumeCategory_ThatOnlyAClientThatSubscribedToItGets()
        {
            var h = new SimHarness();
            h.CreateInput(h.InitialScene, "Mic", Audio);
            h.Settle();
            h.Sim.External.MeterTick();
            h.Settle();

            var message = Assert.Single(h.Events("InputVolumeMeters"));

            Assert.Equal(SimIntent.InputVolumeMeters, message.Intent);
            Assert.NotEqual(0, message.Intent & 65536);
            Assert.Equal(0, SimIntent.All & SimIntent.InputVolumeMeters);
        }
    }
}
