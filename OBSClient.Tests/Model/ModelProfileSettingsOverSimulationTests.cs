namespace OBSStudioClient.Tests.Model
{
    using System.Text.Json;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Model;
    using OBSStudioClient.Tests.Simulation;
    using OBSStudioClient.Tests.TestSupport;

    /// <summary>
    /// The calls about the settings of OBS Studio itself (the video, the stream service, the folder for recordings, the parameters of a profile, the persistent data), over the whole stack: an <see cref="ObsModel"/> on a real
    /// <see cref="ObsClient"/>, connected to the simulation of OBS Studio (<see cref="SimObsServer"/>), with the lags and the order of what OBS Studio really does. The model keeps none of it, so what a call says is what the
    /// simulation holds when it is asked, and it holds the settings of the profile that is current then.
    /// </summary>
    public class ModelProfileSettingsOverSimulationTests
    {
        private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

        private static SimQuirks Quirks(string preset)
        {
            return preset switch
            {
                "Ideal" => ModelSimRig.DefaultQuirks,
                "Real" => SimQuirks.Real,
                _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, null),
            };
        }

        private static async Task<ModelSimRig> StartAsync(SimQuirks quirks, int seed = 5)
        {
            var rig = new ModelSimRig(quirks, null, seed);
            try
            {
                await rig.ConnectAsync().WithTimeoutAsync(Patience);
                await rig.ConvergedAsync("the first read", Patience);
                return rig;
            }
            catch
            {
                await rig.DisposeAsync();
                throw;
            }
        }

        private static (int, int, int, int, int, int) Held(SimVideo video) => (video.FpsNumerator, video.FpsDenominator, video.BaseWidth, video.BaseHeight, video.OutputWidth, video.OutputHeight);

        private static (int, int, int, int, int, int) Said(ObsVideoSettings video) => (video.FpsNumerator, video.FpsDenominator, video.BaseWidth, video.BaseHeight, video.OutputWidth, video.OutputHeight);

        [Theory]
        [InlineData("Ideal")]
        [InlineData("Real")]
        public async Task TheVideoSettings_AreAskedForAndChanged_AsTheSimulationHoldsThem(string preset)
        {
            await using var rig = await StartAsync(Quirks(preset));
            var model = rig.Model;

            var fresh = await model.GetVideoSettingsAsync().WithTimeoutAsync(Patience);
            await model.SetVideoSettingsAsync(baseResolution: (1280, 720), outputResolution: (640, 360), frameRate: (30000, 1001)).WithTimeoutAsync(Patience);
            var changed = await model.GetVideoSettingsAsync().WithTimeoutAsync(Patience);
            await model.SetVideoSettingsAsync(frameRate: (60, 1)).WithTimeoutAsync(Patience);
            var fps = await model.GetVideoSettingsAsync().WithTimeoutAsync(Patience);

            Assert.Equal((30, 1, 1920, 1080, 1280, 720), Said(fresh));
            Assert.Equal((30000, 1001, 1280, 720, 640, 360), Said(changed));
            Assert.Equal((60, 1, 1280, 720, 640, 360), Said(fps));
            Assert.Equal(Held(rig.Server.Run(sim => sim.Video)), Said(fps));
            Assert.Equal("1280", (await model.Profiles.GetParameterAsync("Video", "BaseCX").WithTimeoutAsync(Patience)).Value);
            Assert.Null(rig.Compare());
        }

        [Fact]
        public async Task AValueThatOBSStudioDoesNotTake_IsRefusedWithItsCodeAndItsWords_AndChangesNothing()
        {
            await using var rig = await StartAsync(Quirks("Ideal"));

            var refused = await Assert.ThrowsAsync<ObsResponseException>(() => rig.Model.SetVideoSettingsAsync(baseResolution: (4, 4)).WithTimeoutAsync(Patience));

            Assert.Equal(RequestStatusCode.RequestFieldOutOfRange, refused.ErrorCode);
            Assert.Equal("The field value of `baseWidth` is below the minimum of `8.000000`", refused.Message);
            Assert.Equal((30, 1, 1920, 1080, 1280, 720), Said(await rig.Model.GetVideoSettingsAsync().WithTimeoutAsync(Patience)));
        }

        [Theory]
        [InlineData("Ideal")]
        [InlineData("Real")]
        public async Task TheStreamServiceAndTheRecordDirectory_AreAskedForAndChanged_AsTheSimulationHoldsThem(string preset)
        {
            await using var rig = await StartAsync(Quirks(preset));
            var model = rig.Model;

            var fresh = await model.GetStreamServiceAsync().WithTimeoutAsync(Patience);
            await model.SetStreamServiceAsync("rtmp_custom", new { server = "rtmp://a/app", key = "k1" }).WithTimeoutAsync(Patience);
            await model.SetStreamServiceAsync("rtmp_custom", new { server = "rtmp://b/app" }).WithTimeoutAsync(Patience);
            var custom = await model.GetStreamServiceAsync().WithTimeoutAsync(Patience);
            var noType = await Assert.ThrowsAsync<ObsResponseException>(() => model.SetStreamServiceAsync("no_such_service", new { server = "x" }).WithTimeoutAsync(Patience));
            var folder = await model.GetRecordDirectoryAsync().WithTimeoutAsync(Patience);
            await model.SetRecordDirectoryAsync(@"D:\Recordings").WithTimeoutAsync(Patience);

            Assert.Equal("rtmp_common", fresh.Type);
            Assert.Equal(JsonValueKind.Object, fresh.Settings.ValueKind);
            Assert.False(fresh.Settings.EnumerateObject().Any());
            Assert.Equal("rtmp_custom", custom.Type);
            Assert.Equal("rtmp://b/app", custom.Settings.GetProperty("server").GetString());
            Assert.Equal("k1", custom.Settings.GetProperty("key").GetString());
            Assert.Equal(RequestStatusCode.ResourceCreationFailed, noType.ErrorCode);
            Assert.Equal(SimObs.DefaultRecordDirectory, folder);
            Assert.Equal(@"D:\Recordings", await model.GetRecordDirectoryAsync().WithTimeoutAsync(Patience));
            Assert.Equal("rtmp_custom", rig.Server.Run(sim => sim.ServiceType));
            Assert.Equal(@"D:\Recordings", rig.Server.Run(sim => sim.RecordDirectory));
            Assert.Null(rig.Compare());
        }

        [Theory]
        [InlineData("Ideal")]
        [InlineData("Real")]
        public async Task TheParametersOfTheProfile_AreAskedForSetAndRemoved_AsTheSimulationHoldsThem(string preset)
        {
            await using var rig = await StartAsync(Quirks(preset));
            var profiles = rig.Model.Profiles;

            var mode = await profiles.GetParameterAsync("Output", "Mode").WithTimeoutAsync(Patience);
            var nobody = await profiles.GetParameterAsync("walk", "a").WithTimeoutAsync(Patience);
            await profiles.SetParameterAsync("walk", "a", "1").WithTimeoutAsync(Patience);
            var set = await profiles.GetParameterAsync("walk", "a").WithTimeoutAsync(Patience);
            await profiles.SetParameterAsync("walk", "a", null).WithTimeoutAsync(Patience);
            var removed = await profiles.GetParameterAsync("walk", "a").WithTimeoutAsync(Patience);
            var again = await Assert.ThrowsAsync<ObsNotFoundException>(() => profiles.SetParameterAsync("walk", "a", null).WithTimeoutAsync(Patience));

            Assert.Equal(("Simple", "Simple"), (mode.Value, mode.DefaultValue));
            Assert.False(nobody.HasValue);
            Assert.Equal(("1", null), (set.Value, set.DefaultValue));
            Assert.False(removed.HasValue);
            Assert.Equal("profile parameter", again.Kind);
            Assert.Equal("walk/a", again.RequestedName);
        }

        [Theory]
        [InlineData("Ideal")]
        [InlineData("Real")]
        public async Task ThePersistentData_IsKeptForTheProfile_OrForNone(string preset)
        {
            await using var rig = await StartAsync(Quirks(preset));
            var model = rig.Model;

            Assert.Null(await model.GetPersistentDataAsync(ObsDataRealm.Global, "slot").WithTimeoutAsync(Patience));
            await model.SetPersistentDataAsync(ObsDataRealm.Global, "slot", new { b = 2, a = new[] { 1, 2 } }).WithTimeoutAsync(Patience);
            await model.SetPersistentDataAsync(ObsDataRealm.Profile, "slot", "of the profile").WithTimeoutAsync(Patience);
            var global = await model.GetPersistentDataAsync(ObsDataRealm.Global, "slot").WithTimeoutAsync(Patience);
            var profile = await model.GetPersistentDataAsync(ObsDataRealm.Profile, "slot").WithTimeoutAsync(Patience);
            await model.SetPersistentDataAsync(ObsDataRealm.Global, "slot", string.Empty).WithTimeoutAsync(Patience);
            var emptied = await model.GetPersistentDataAsync(ObsDataRealm.Global, "slot").WithTimeoutAsync(Patience);

            Assert.Equal("""{"a":[1,2],"b":2}""", global!.Value.GetRawText());
            Assert.Equal("of the profile", profile!.Value.GetString());
            Assert.Equal(string.Empty, emptied!.Value.GetString());
        }

        [Theory]
        [InlineData("Ideal")]
        [InlineData("Real")]
        public async Task TheSettings_AreThoseOfTheProfileThatIsCurrent_AndTheGlobalPersistentDataIsTheSameInEveryProfile(string preset)
        {
            await using var rig = await StartAsync(Quirks(preset));
            var model = rig.Model;
            await model.SetVideoSettingsAsync(frameRate: (24, 1)).WithTimeoutAsync(Patience);
            await model.SetRecordDirectoryAsync(@"D:\Untitled").WithTimeoutAsync(Patience);
            await model.SetPersistentDataAsync(ObsDataRealm.Profile, "slot", "untitled").WithTimeoutAsync(Patience);
            await model.SetPersistentDataAsync(ObsDataRealm.Global, "slot", "everywhere").WithTimeoutAsync(Patience);

            await model.Profiles.CreateAsync("Second").WithTimeoutAsync(Patience);

            // A profile that is made is a clean one, and what the model says is what the profile that is current holds.
            Assert.Equal((30, 1, 1920, 1080, 1280, 720), Said(await model.GetVideoSettingsAsync().WithTimeoutAsync(Patience)));
            Assert.Equal(SimObs.DefaultRecordDirectory, await model.GetRecordDirectoryAsync().WithTimeoutAsync(Patience));
            Assert.Null(await model.GetPersistentDataAsync(ObsDataRealm.Profile, "slot").WithTimeoutAsync(Patience));
            Assert.Equal("everywhere", (await model.GetPersistentDataAsync(ObsDataRealm.Global, "slot").WithTimeoutAsync(Patience))!.Value.GetString());
            await model.SetVideoSettingsAsync(frameRate: (50, 1)).WithTimeoutAsync(Patience);

            await model.Profiles.SetCurrentAsync("Untitled").WithTimeoutAsync(Patience);

            Assert.Equal((24, 1, 1920, 1080, 1280, 720), Said(await model.GetVideoSettingsAsync().WithTimeoutAsync(Patience)));
            Assert.Equal(@"D:\Untitled", await model.GetRecordDirectoryAsync().WithTimeoutAsync(Patience));
            Assert.Equal("untitled", (await model.GetPersistentDataAsync(ObsDataRealm.Profile, "slot").WithTimeoutAsync(Patience))!.Value.GetString());
            Assert.Equal("everywhere", (await model.GetPersistentDataAsync(ObsDataRealm.Global, "slot").WithTimeoutAsync(Patience))!.Value.GetString());

            await model.Profiles.SetCurrentAsync("Second").WithTimeoutAsync(Patience);
            Assert.Equal((50, 1, 1920, 1080, 1280, 720), Said(await model.GetVideoSettingsAsync().WithTimeoutAsync(Patience)));
            Assert.Null(rig.Compare());
        }

        [Fact]
        public async Task TheCallsThatAskAndTheCallsThatChange_AreMadeInTheOrderOfTheCalls_EachInTheProfileThatTheSwitchesBeforeItLeftCurrent()
        {
            await using var rig = await StartAsync(Quirks("Ideal"));
            var model = rig.Model;
            await model.SetVideoSettingsAsync(frameRate: (24, 1)).WithTimeoutAsync(Patience);
            await model.Profiles.CreateAsync("Second").WithTimeoutAsync(Patience);
            await model.SetVideoSettingsAsync(frameRate: (50, 1)).WithTimeoutAsync(Patience);

            var switchBack = model.Profiles.SetCurrentAsync("Untitled");
            var inUntitled = model.GetVideoSettingsAsync();
            var change = model.SetVideoSettingsAsync(frameRate: (25, 1));
            var switchForward = model.Profiles.SetCurrentAsync("Second");
            var inSecond = model.GetVideoSettingsAsync();
            await Task.WhenAll(switchBack, inUntitled, change, switchForward, inSecond).WithTimeoutAsync(Patience);

            Assert.Equal(24, (await inUntitled).FpsNumerator);
            Assert.Equal(50, (await inSecond).FpsNumerator);
            await model.Profiles.SetCurrentAsync("Untitled").WithTimeoutAsync(Patience);
            Assert.Equal(25, (await model.GetVideoSettingsAsync().WithTimeoutAsync(Patience)).FpsNumerator);
        }
    }
}
