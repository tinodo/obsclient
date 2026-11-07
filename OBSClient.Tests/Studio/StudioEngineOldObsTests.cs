namespace OBSStudioClient.Tests.Studio
{
    using OBSStudioClient.Studio.Engine;

    /// <summary>
    /// What the model does with an OBS Studio whose obs-websocket is older than 5.6.0. That one does not send the capabilities of the input kinds (the
    /// list of inputs and the InputCreated event do not have them), so the model finds out whether an input has audio by reading its audio state, which
    /// OBS refuses to give for an input without audio.
    /// </summary>
    public class StudioEngineOldObsTests
    {
        private static EngineHarness WithWorld(Action<FakeWorld>? setup = null, bool sendsCapabilities = false)
        {
            var harness = new EngineHarness();
            harness.World.OmitInputKindCaps = !sendsCapabilities;
            harness.World.AddScene("Scene");
            setup?.Invoke(harness.World);
            return harness;
        }

        // Answers what the world says, and counts the reads of the audio state of each input.
        private static Func<ReadRequest, (int Code, object? Data)> CountingAudioReads(EngineHarness harness, Dictionary<Guid, int> counts)
        {
            return request =>
            {
                if (request.Kind == ReadKind.AudioMute)
                {
                    counts[request.Target] = counts.GetValueOrDefault(request.Target) + 1;
                }

                return harness.World.Answer(request);
            };
        }

        [Fact]
        public void TheInputs_AreInTheModel_WithAudioWhereTheyHaveIt()
        {
            Guid mic = Guid.Empty;
            Guid color = Guid.Empty;
            var counts = new Dictionary<Guid, int>();
            var h = WithWorld(world =>
            {
                mic = world.AddInput("Mic", "wasapi_input_capture", 2, muted: true, volumeMul: 0.5);
                color = world.AddInput("Color");
            });
            h.Start();

            h.SettleWithTime(CountingAudioReads(h, counts));

            var snapshot = h.Engine.Snapshot();
            Assert.Equal(StudioEngineState.Synchronized, snapshot.State);
            Assert.Equal(0, snapshot.Incomplete);
            Assert.True(h.Engine.IsSettled);
            var micInput = snapshot.Inputs.Single(input => input.Uuid == mic);
            Assert.Equal((true, true, 0.5f), (micInput.HasAudio, micInput.Muted, micInput.VolumeMul));
            Assert.False(snapshot.Inputs.Single(input => input.Uuid == color).HasAudio);

            // The refusal of OBS is an answer, not a failure: it is not asked again.
            Assert.Equal((1, 1), (counts[mic], counts[color]));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void AnInputThatAnEventCreates_IsAnnounced_WhenItsAudioStateIsKnown(bool hasAudio)
        {
            var h = WithWorld();
            h.Start();
            h.SettleWithTime();
            h.Engine.TakeChanges();
            var uuid = h.World.AddInput("New", hasAudio ? "wasapi_input_capture" : "color_source_v3", hasAudio ? 2u : 32777u, muted: hasAudio);

            // The event does not have the capabilities either.
            h.Event("InputCreated", new { inputName = "New", inputUuid = uuid, inputKind = "kind", unversionedInputKind = "kind", inputSettings = new { }, defaultInputSettings = new { } }, 8);

            // Not before its audio state is read: until then the model cannot say whether the input has audio.
            Assert.DoesNotContain(h.Engine.TakeChanges(), change => change.Kind == StudioChangeKind.InputAdded);
            Assert.DoesNotContain(h.Engine.Snapshot().Inputs, input => input.Uuid == uuid);

            h.SettleWithTime();

            var input = Assert.Single(h.Engine.Snapshot().Inputs, i => i.Uuid == uuid);
            Assert.Equal((hasAudio, hasAudio), (input.HasAudio, input.Muted));
            var changes = h.Engine.TakeChanges();
            Assert.Single(changes, change => change.Kind == StudioChangeKind.InputAdded);
            Assert.DoesNotContain(changes, change => change.Kind == StudioChangeKind.Repaired);
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void AfterAReconnect_AnInputThatOBSSaidHasNoAudio_IsNotAskedAgain_AndTheOneWithAudioIs()
        {
            Guid mic = Guid.Empty;
            Guid color = Guid.Empty;
            var counts = new Dictionary<Guid, int>();
            var h = WithWorld(world =>
            {
                mic = world.AddInput("Mic", "wasapi_input_capture", 2);
                color = world.AddInput("Color");
            });
            h.Start();
            h.SettleWithTime();

            h.End();
            h.Start();
            h.SettleWithTime(CountingAudioReads(h, counts));

            var snapshot = h.Engine.Snapshot();
            Assert.Equal(StudioEngineState.Synchronized, snapshot.State);
            Assert.True(snapshot.Inputs.Single(input => input.Uuid == mic).HasAudio);
            Assert.False(snapshot.Inputs.Single(input => input.Uuid == color).HasAudio);
            Assert.Equal(1, counts[mic]);
            Assert.False(counts.ContainsKey(color));
        }

        [Fact]
        public void AnObsThatSendsTheCapabilitiesAfterAReconnect_GivesTheSameInputs()
        {
            Guid mic = Guid.Empty;
            Guid color = Guid.Empty;
            var h = WithWorld(world =>
            {
                mic = world.AddInput("Mic", "wasapi_input_capture", 2);
                color = world.AddInput("Color");
            });
            h.Start();
            h.SettleWithTime();
            Assert.Equal(0, h.Engine.Snapshot().Incomplete);

            h.End();
            h.World.OmitInputKindCaps = false;
            h.Start();
            h.SettleWithTime();

            var snapshot = h.Engine.Snapshot();
            Assert.Equal(StudioEngineState.Synchronized, snapshot.State);
            Assert.Equal(2, snapshot.Inputs.Count);
            Assert.True(snapshot.Inputs.Single(input => input.Uuid == mic).HasAudio);
            Assert.False(snapshot.Inputs.Single(input => input.Uuid == color).HasAudio);
        }

        [Fact]
        public void WhenTheCapabilitiesAreSent_TheAudioStateOfAnInputWithoutAudio_IsNotAskedFor()
        {
            Guid mic = Guid.Empty;
            Guid color = Guid.Empty;
            var counts = new Dictionary<Guid, int>();
            var h = WithWorld(
                world =>
                {
                    mic = world.AddInput("Mic", "wasapi_input_capture", 2);
                    color = world.AddInput("Color");
                },
                sendsCapabilities: true);
            h.Start();

            h.SettleWithTime(CountingAudioReads(h, counts));

            Assert.Equal(1, counts[mic]);
            Assert.False(counts.ContainsKey(color));
            Assert.Equal(0, h.Engine.Snapshot().Incomplete);
        }
    }
}
