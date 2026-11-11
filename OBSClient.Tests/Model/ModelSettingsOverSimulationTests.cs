namespace OBSStudioClient.Tests.Model
{
    using System.Text.Json.Nodes;
    using OBSStudioClient.Model;
    using OBSStudioClient.Tests.Simulation;
    using OBSStudioClient.Tests.TestSupport;

    /// <summary>
    /// The settings of the inputs and the details of their audio, over the whole stack: an <see cref="ObsModel"/> on a real <see cref="ObsClient"/>, connected to the simulation of OBS Studio
    /// (<see cref="SimObsServer"/>), with the lags and the order of what OBS Studio really does. What the model shows is what the simulation holds (<see cref="ModelMirror"/>), after what
    /// someone else changed and after what the application asked for.
    /// </summary>
    public class ModelSettingsOverSimulationTests
    {
        private const string Mic = "wasapi_input_capture";
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

        // A scene "Gaming" with a backdrop (a color source, which has settings and no audio) and a microphone.
        private static async Task<ModelSimRig> StartAsync(SimQuirks? quirks = null, int seed = 11)
        {
            var rig = new ModelSimRig(quirks, null, seed);
            try
            {
                await rig.ConnectAsync().WithTimeoutAsync(Patience);
                await rig.ConvergedAsync("the first read", Patience);
                rig.World(world =>
                {
                    var gaming = world.CreateScene("Gaming");
                    world.CreateInput(gaming, "Backdrop", Colour);
                    world.CreateInput(gaming, "Mic", Mic);
                });
                await rig.ConvergedAsync("the scene and the inputs", Patience);
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
        public async Task TheSettingsAndTheAudioDetails_FollowWhatSomeoneElseChanges(string preset)
        {
            await using var rig = await StartAsync(Quirks(preset));
            var backdrop = rig.Model.Inputs["Backdrop"];
            var mic = rig.Model.Inputs["Mic"];
            Assert.Equal("{}", backdrop.Settings.GetRawText());
            Assert.Equal((0.5f, TimeSpan.Zero, ObsMonitorType.Off, ObsAudioTracks.All), (mic.AudioBalance, mic.SyncOffset, mic.MonitorType, mic.AudioTracks));

            rig.World(world => world.SetSettings(backdrop.Uuid, new JsonObject { ["width"] = 800, ["font"] = new JsonObject { ["size"] = 24 } }));
            await rig.ConvergedAsync("the settings of the backdrop", Patience);
            rig.World(world =>
            {
                world.SetBalance(mic.Uuid, 0.25f);
                world.SetSyncOffset(mic.Uuid, 150);

                // OBS Studio 33.0.0 holds monitor and output when it is told monitor only.
                world.SetMonitorType(mic.Uuid, "OBS_MONITORING_TYPE_MONITOR_ONLY");
                world.SetTracks(mic.Uuid, 0b100101);
            });
            await rig.ConvergedAsync("the audio details of the microphone", Patience);

            Assert.Equal(800, backdrop.Settings.GetProperty("width").GetInt32());
            Assert.Equal(24, backdrop.Settings.GetProperty("font").GetProperty("size").GetInt32());
            Assert.Equal((0.25f, TimeSpan.FromMilliseconds(150), ObsMonitorType.MonitorAndOutput, ObsAudioTracks.Track1 | ObsAudioTracks.Track3 | ObsAudioTracks.Track6), (mic.AudioBalance, mic.SyncOffset, mic.MonitorType, mic.AudioTracks));
            Assert.Contains(rig.Changes, change => ReferenceEquals(change.Entity, backdrop) && change.PropertyName == "Settings");
            Assert.Contains(rig.Changes, change => ReferenceEquals(change.Entity, mic) && change.PropertyName == "AudioTracks");
        }

        [Theory]
        [InlineData("Ideal")]
        [InlineData("Real")]
        [InlineData("Hostile")]
        public async Task EveryChangeOfTheSettingsAndTheAudioDetails_IsShownByTheModel_WhenTheCallReturns(string preset)
        {
            await using var rig = await StartAsync(Quirks(preset));
            var backdrop = rig.Model.Inputs["Backdrop"];
            var mic = rig.Model.Inputs["Mic"];

            await backdrop.SetSettingsAsync(new { width = 640, height = 360 }).WithTimeoutAsync(Patience);
            Assert.Equal(640, backdrop.Settings.GetProperty("width").GetInt32());
            await backdrop.SetSettingsAsync(new { height = 720 }).WithTimeoutAsync(Patience);
            Assert.Equal(640, backdrop.Settings.GetProperty("width").GetInt32());
            Assert.Equal(720, backdrop.Settings.GetProperty("height").GetInt32());
            await rig.ConvergedAsync("the settings of the backdrop, laid over the ones that it had", Patience);

            await backdrop.SetSettingsAsync(new { color = 4278190335L }, overlay: false).WithTimeoutAsync(Patience);
            Assert.Equal("""{"color":4278190335}""", backdrop.Settings.GetRawText());
            await rig.ConvergedAsync("the settings of the backdrop, after a reset", Patience);

            await mic.SetAudioBalanceAsync(0.75f).WithTimeoutAsync(Patience);
            Assert.Equal(0.75f, mic.AudioBalance);
            await mic.SetSyncOffsetAsync(TimeSpan.FromMilliseconds(-200)).WithTimeoutAsync(Patience);
            Assert.Equal(TimeSpan.FromMilliseconds(-200), mic.SyncOffset);
            await mic.SetMonitorTypeAsync(ObsMonitorType.MonitorAndOutput).WithTimeoutAsync(Patience);
            Assert.Equal(ObsMonitorType.MonitorAndOutput, mic.MonitorType);
            await mic.SetAudioTracksAsync(ObsAudioTracks.Track2 | ObsAudioTracks.Track4).WithTimeoutAsync(Patience);
            Assert.Equal(ObsAudioTracks.Track2 | ObsAudioTracks.Track4, mic.AudioTracks);

            // The same again: nothing changes, and the calls end all the same (OBS Studio says nothing for a monitor type and tracks that are what they were).
            await mic.SetMonitorTypeAsync(ObsMonitorType.MonitorAndOutput).WithTimeoutAsync(Patience);
            await mic.SetAudioTracksAsync(ObsAudioTracks.Track2 | ObsAudioTracks.Track4).WithTimeoutAsync(Patience);
            await mic.SetAudioBalanceAsync(0.75f).WithTimeoutAsync(Patience);
            await rig.ConvergedAsync("the audio details of the microphone", Patience);
        }

        [Theory]
        [InlineData("Ideal")]
        [InlineData("Real")]
        [InlineData("Hostile")]
        public async Task AMonitorTypeThatOBSStudioTurnsIntoAnother_EndsTheCall_AndTheModelShowsWhatOBSStudioHolds(string preset)
        {
            await using var rig = await StartAsync(Quirks(preset));
            var mic = rig.Model.Inputs["Mic"];

            // OBS Studio 33.0.0 has no monitor only: it holds monitor and output, and says so.
            await mic.SetMonitorTypeAsync(ObsMonitorType.MonitorOnly).WithTimeoutAsync(Patience);
            Assert.Equal(ObsMonitorType.MonitorAndOutput, mic.MonitorType);
            await rig.ConvergedAsync("the monitor type of the microphone", Patience);

            await mic.SetMonitorTypeAsync(ObsMonitorType.Off).WithTimeoutAsync(Patience);
            Assert.Equal(ObsMonitorType.Off, mic.MonitorType);
            await rig.ConvergedAsync("the monitor type of the microphone, off again", Patience);
        }

        [Theory]
        [InlineData("Ideal")]
        [InlineData("Real")]
        [InlineData("Hostile")]
        public async Task ANullFieldOfTheSettings_IsIgnoredByOBSStudio_AndTheModelShowsIt(string preset)
        {
            await using var rig = await StartAsync(Quirks(preset));
            var backdrop = rig.Model.Inputs["Backdrop"];
            await backdrop.SetSettingsAsync(new { width = 640, font = new { face = "Arial", size = 48 } }).WithTimeoutAsync(Patience);

            await backdrop.SetSettingsAsync(new Dictionary<string, object?> { ["width"] = null, ["height"] = 360, ["font"] = new { size = 72 } }).WithTimeoutAsync(Patience);

            Assert.Equal("""{"font":{"size":72},"height":360,"width":640}""", backdrop.Settings.GetRawText());
            await rig.ConvergedAsync("the settings with a null field", Patience);
        }

        [Fact]
        public async Task SeveralChangesOfTheSameSettings_ThatAreAskedForRightAfterOneAnother_ArriveInOrder()
        {
            await using var rig = await StartAsync(Quirks("Hostile"));
            var backdrop = rig.Model.Inputs["Backdrop"];

            var changes = Enumerable.Range(1, 8).Select(width => backdrop.SetSettingsAsync(new { width = width * 100 })).ToArray();
            await Task.WhenAll(changes).WithTimeoutAsync(Patience);

            Assert.Equal(800, backdrop.Settings.GetProperty("width").GetInt32());
            await rig.ConvergedAsync("the settings after eight changes", Patience);
        }
    }
}
