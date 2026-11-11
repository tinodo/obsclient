namespace OBSStudioClient.Tests.Live
{
    using System.Text.Json;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Model;
    using OBSStudioClient.Tests.TestSupport;
    using Xunit.Abstractions;

    /// <summary>
    /// What the model does with the settings of OBS Studio itself against a real OBS Studio: the video settings, the stream service, the folder for recordings, the parameters of a profile and the data that OBS Studio
    /// keeps for an application are asked for and changed with calls of the model, they are those of the profile that is current, and OBS Studio refuses to change some of them while an output runs. All the changes are made
    /// in a profile that the test makes and removes again (the profile that was current is put back), and the data that the test keeps is emptied (OBS Studio can not remove it). The recording goes to a folder that the test makes
    /// and removes.
    /// </summary>
    [Collection("Live OBS")]
    public class LiveProfileSettingsModelTests(ITestOutputHelper output)
    {
        private static readonly TimeSpan Wait = TimeSpan.FromSeconds(30);

        private static async Task RestoreAsync(ObsClient client, string profile, string? made)
        {
            try
            {
                if ((await client.GetProfileList()).CurrentProfileName != profile)
                {
                    await client.SetCurrentProfile(profile);
                    await Task.Delay(1000);
                }

                if (made is not null && (await client.GetProfileList()).Profiles.Contains(made))
                {
                    await client.RemoveProfile(made);
                    await Task.Delay(500);
                }
            }
            catch (ObsClientException exception)
            {
                // Nothing more can be done for it here.
                _ = exception;
            }
        }

        private static async Task DeleteFolderAsync(string folder)
        {
            for (var attempt = 0; attempt < 20 && Directory.Exists(folder); attempt++)
            {
                try
                {
                    Directory.Delete(folder, recursive: true);
                }
                catch (IOException)
                {
                    // OBS Studio still has the file open for a moment.
                    await Task.Delay(250);
                }
            }
        }

        private static string Fields(JsonElement settings) => string.Join(", ", settings.EnumerateObject().Select(field => field.Name));

        [LiveUuidChangesFact]
        public async Task TheSettingsOfAProfile_AreAskedForAndChanged_AndBelongToTheProfile()
        {
            await using var live = await LiveModel.ConnectAsync(output);
            var model = live.Model;
            var client = live.Client;
            output.WriteLine($"OBS Studio {live.ObsVersionText}, obs-websocket {live.WebSocketVersionText}.");
            var original = model.Profiles.Current!;
            var made = LiveObs.UniqueName("ps");
            var slot = LiveObs.UniqueName("slot");
            var folder = Path.Combine(Path.GetTempPath(), "obsclient-live-rec-" + Guid.NewGuid().ToString("N")[..8]);

            // What the profile that is current holds, as the model and as the client say it.
            var originalVideo = await model.GetVideoSettingsAsync();
            var viaClient = await client.GetVideoSettings();
            output.WriteLine($"The profile '{original}': {originalVideo}.");
            Assert.Equal((int)viaClient.FpsNumerator, originalVideo.FpsNumerator);
            Assert.Equal((int)viaClient.FpsDenominator, originalVideo.FpsDenominator);
            Assert.Equal(viaClient.BaseWidth, originalVideo.BaseWidth);
            Assert.Equal(viaClient.BaseHeight, originalVideo.BaseHeight);
            Assert.Equal(viaClient.OutputWidth, originalVideo.OutputWidth);
            Assert.Equal(viaClient.OutputHeight, originalVideo.OutputHeight);
            var originalService = await model.GetStreamServiceAsync();
            var originalFolder = await model.GetRecordDirectoryAsync();
            Assert.Equal((await client.GetStreamServiceSettings()).StreamServiceType, originalService.Type);
            Assert.Equal(await client.GetRecordDirectory(), originalFolder);

            try
            {
                Directory.CreateDirectory(folder);
                await model.Profiles.CreateAsync(made).WaitAsync(Wait);
                Assert.Equal(made, model.Profiles.Current);
                var fresh = await model.GetVideoSettingsAsync();
                var freshService = await model.GetStreamServiceAsync();
                output.WriteLine($"The new profile '{made}': {fresh}; {freshService} with the fields {Fields(freshService.Settings)}; the folder is {await model.GetRecordDirectoryAsync()}.");

                // The video settings: what is given changes, the rest stays; the frame rate is a fraction.
                await model.SetVideoSettingsAsync(baseResolution: (1280, 720), outputResolution: (640, 360), frameRate: (30000, 1001));
                var video = await model.GetVideoSettingsAsync();
                output.WriteLine($"After the change: {video}.");
                Assert.Equal((1280, 720, 640, 360, 30000, 1001), (video.BaseWidth, video.BaseHeight, video.OutputWidth, video.OutputHeight, video.FpsNumerator, video.FpsDenominator));
                Assert.Equal(30000d / 1001, video.FramesPerSecond, 6);
                await model.SetVideoSettingsAsync(frameRate: (60, 1));
                video = await model.GetVideoSettingsAsync();
                Assert.Equal((1280, 720, 640, 360, 60, 1), (video.BaseWidth, video.BaseHeight, video.OutputWidth, video.OutputHeight, video.FpsNumerator, video.FpsDenominator));
                await model.SetVideoSettingsAsync(outputResolution: (1280, 720));
                video = await model.GetVideoSettingsAsync();
                Assert.Equal((1280, 720, 1280, 720, 60, 1), (video.BaseWidth, video.BaseHeight, video.OutputWidth, video.OutputHeight, video.FpsNumerator, video.FpsDenominator));

                // OBS Studio keeps them in the configuration of the profile, as text, and says so when it is asked for the parameters.
                Assert.Equal("1280", (await model.Profiles.GetParameterAsync("Video", "BaseCX")).Value);
                Assert.Equal("720", (await model.Profiles.GetParameterAsync("Video", "BaseCY")).Value);
                Assert.Equal("1280", (await model.Profiles.GetParameterAsync("Video", "OutputCX")).Value);
                Assert.Equal("60", (await model.Profiles.GetParameterAsync("Video", "FPSNum")).Value);
                Assert.Equal("1", (await model.Profiles.GetParameterAsync("Video", "FPSDen")).Value);

                // A size that OBS Studio does not take (the least is 8) is refused with the code of the refusal; the call itself takes any size that obs-websocket documents.
                var tooSmall = await Assert.ThrowsAsync<ObsResponseException>(() => model.SetVideoSettingsAsync(baseResolution: (4, 4)));
                output.WriteLine($"A canvas of 4 by 4: {tooSmall.ErrorCode}: {tooSmall.Message}");
                Assert.Equal(RequestStatusCode.RequestFieldOutOfRange, tooSmall.ErrorCode);
                Assert.Equal(1280, (await model.GetVideoSettingsAsync()).BaseWidth);

                // The stream service: the type and the settings, and settings of the same type are laid over the ones that it holds.
                var key = "not-a-key-" + Guid.NewGuid().ToString("N")[..6];
                await model.SetStreamServiceAsync("rtmp_custom", new { server = "rtmp://127.0.0.1/obsclient-live", key });
                var service = await model.GetStreamServiceAsync();
                output.WriteLine($"After the change: {service} with the fields {Fields(service.Settings)}.");
                Assert.Equal("rtmp_custom", service.Type);
                Assert.Equal("rtmp://127.0.0.1/obsclient-live", service.Settings.GetProperty("server").GetString());
                Assert.Equal(key, service.Settings.GetProperty("key").GetString());
                Assert.DoesNotContain(key, service.ToString());
                await model.SetStreamServiceAsync("rtmp_custom", new { server = "rtmp://127.0.0.1/other" });
                service = await model.GetStreamServiceAsync();
                Assert.Equal("rtmp://127.0.0.1/other", service.Settings.GetProperty("server").GetString());
                Assert.Equal(key, service.Settings.GetProperty("key").GetString());
                var noService = await Assert.ThrowsAsync<ObsResponseException>(() => model.SetStreamServiceAsync("no_such_service", new { server = "x" }));
                output.WriteLine($"A service that does not exist: {noService.ErrorCode}: {noService.Message}");
                Assert.Equal(RequestStatusCode.ResourceCreationFailed, noService.ErrorCode);
                var emptySettings = await Assert.ThrowsAsync<ObsResponseException>(() => model.SetStreamServiceAsync("rtmp_custom", new { }));
                output.WriteLine($"Settings with no field: {emptySettings.ErrorCode}: {emptySettings.Message}");
                Assert.Equal(RequestStatusCode.RequestFieldEmpty, emptySettings.ErrorCode);
                Assert.Equal("rtmp://127.0.0.1/other", (await model.GetStreamServiceAsync()).Settings.GetProperty("server").GetString());

                // The folder for recordings: OBS Studio sets the one of both output modes.
                await model.SetRecordDirectoryAsync(folder);
                Assert.Equal(folder, await model.GetRecordDirectoryAsync());
                Assert.Equal(folder, (await model.Profiles.GetParameterAsync("SimpleOutput", "FilePath")).Value);
                Assert.Equal(folder, (await model.Profiles.GetParameterAsync("AdvOut", "RecFilePath")).Value);

                // The parameters of the profile: a parameter that nobody set has no value, and no error; one that is set is read back, and removing it takes the value away.
                var probe = await model.Profiles.GetParameterAsync("obsclient-live", "probe");
                Assert.False(probe.HasValue);
                Assert.Null(probe.Value);
                Assert.Null(probe.DefaultValue);
                Assert.Equal("obsclient-live", probe.Category);
                Assert.Equal("probe", probe.Name);
                await model.Profiles.SetParameterAsync("obsclient-live", "probe", "1");
                probe = await model.Profiles.GetParameterAsync("obsclient-live", "probe");
                Assert.True(probe.HasValue);
                Assert.Equal("1", probe.Value);
                Assert.Null(probe.DefaultValue);
                output.WriteLine($"The parameter: {probe}. Asked for as OBSCLIENT-LIVE/PROBE: {await model.Profiles.GetParameterAsync("OBSCLIENT-LIVE", "PROBE")}.");
                await model.Profiles.SetParameterAsync("obsclient-live", "probe", null);
                Assert.False((await model.Profiles.GetParameterAsync("obsclient-live", "probe")).HasValue);
                var nothing = await Assert.ThrowsAsync<ObsNotFoundException>(() => model.Profiles.SetParameterAsync("obsclient-live", "probe", null));
                Assert.Equal("profile parameter", nothing.Kind);
                Assert.Equal("obsclient-live/probe", nothing.RequestedName);

                // OBS Studio only writes what a parameter says: what it runs with does not change until it reads its configuration again.
                var runningWidth = (await model.GetVideoSettingsAsync()).BaseWidth;
                await model.Profiles.SetParameterAsync("Video", "BaseCX", "1111");
                Assert.Equal(runningWidth, (await model.GetVideoSettingsAsync()).BaseWidth);
                Assert.Equal("1111", (await model.Profiles.GetParameterAsync("Video", "BaseCX")).Value);
                await model.SetVideoSettingsAsync(baseResolution: (1280, 720));
                Assert.Equal("1280", (await model.Profiles.GetParameterAsync("Video", "BaseCX")).Value);

                // The data that OBS Studio keeps for an application: nothing in a slot that nobody used, and values of every kind that JSON has (a null is not one of them).
                Assert.Null(await model.GetPersistentDataAsync(ObsDataRealm.Global, slot));
                Assert.Null(await model.GetPersistentDataAsync(ObsDataRealm.Profile, slot));
                await model.SetPersistentDataAsync(ObsDataRealm.Global, slot, new { a = 1, list = new[] { 1, 2, 3 } });
                await model.SetPersistentDataAsync(ObsDataRealm.Profile, slot, "text of the profile");
                var global = await model.GetPersistentDataAsync(ObsDataRealm.Global, slot);
                Assert.Equal(1, global!.Value.GetProperty("a").GetInt32());
                Assert.Equal(3, global.Value.GetProperty("list").GetArrayLength());
                Assert.Equal("text of the profile", (await model.GetPersistentDataAsync(ObsDataRealm.Profile, slot))!.Value.GetString());
                await model.SetPersistentDataAsync(ObsDataRealm.Profile, slot, 3.5);
                Assert.Equal(3.5, (await model.GetPersistentDataAsync(ObsDataRealm.Profile, slot))!.Value.GetDouble());
                await model.SetPersistentDataAsync(ObsDataRealm.Profile, slot, true);
                Assert.True((await model.GetPersistentDataAsync(ObsDataRealm.Profile, slot))!.Value.GetBoolean());
                await model.SetPersistentDataAsync(ObsDataRealm.Profile, slot, "stays with the profile");

                // The settings belong to the profile: the profile that was there has its own, the data of the profile realm is the profile's, and the data of the global realm is the same.
                await model.Profiles.SetCurrentAsync(original).WaitAsync(Wait);
                var back = await model.GetVideoSettingsAsync();
                Assert.Equal(originalVideo.ToString(), back.ToString());
                Assert.Equal(originalService.Type, (await model.GetStreamServiceAsync()).Type);
                Assert.Equal(originalFolder, await model.GetRecordDirectoryAsync());
                Assert.Null(await model.GetPersistentDataAsync(ObsDataRealm.Profile, slot));
                Assert.Equal(1, (await model.GetPersistentDataAsync(ObsDataRealm.Global, slot))!.Value.GetProperty("a").GetInt32());
                await model.Profiles.SetCurrentAsync(made).WaitAsync(Wait);
                Assert.Equal("stays with the profile", (await model.GetPersistentDataAsync(ObsDataRealm.Profile, slot))!.Value.GetString());
                Assert.Equal(1280, (await model.GetVideoSettingsAsync()).BaseWidth);

                // The requests are made in the order of the calls: a read that is asked for before a switch of the profile is made in the profile that was current.
                await model.Profiles.SetCurrentAsync(original).WaitAsync(Wait);
                var inOrder = model.GetVideoSettingsAsync();
                await model.Profiles.SetCurrentAsync(made).WaitAsync(Wait);
                Assert.Equal(originalVideo.ToString(), (await inOrder).ToString());
            }
            finally
            {
                try
                {
                    await model.SetPersistentDataAsync(ObsDataRealm.Global, slot, string.Empty);
                }
                catch (ObsClientException exception)
                {
                    output.WriteLine($"Could not empty the global data: {exception.Message}");
                }

                await RestoreAsync(client, original, made);
                await DeleteFolderAsync(folder);
            }
        }

        [LiveUuidChangesFact]
        public async Task TheVideoSettingsAndTheFolderForRecordings_AreRefusedWhileTheOutputRecords()
        {
            await using var live = await LiveModel.ConnectAsync(output);
            var model = live.Model;
            var client = live.Client;
            var record = model.Outputs.Record;
            Assert.False(record.IsActive, "The OBS Studio of the live tests is recording already.");
            var original = model.Profiles.Current!;
            var made = LiveObs.UniqueName("po");
            var folder = Path.Combine(Path.GetTempPath(), "obsclient-live-rec-" + Guid.NewGuid().ToString("N")[..8]);
            var other = Path.Combine(Path.GetTempPath(), "obsclient-live-other-" + Guid.NewGuid().ToString("N")[..8]);
            try
            {
                Directory.CreateDirectory(folder);
                await model.Profiles.CreateAsync(made).WaitAsync(Wait);

                // Nothing is recorded before the folder is the one of this test: a recording that went to the videos folder of the user would stay there.
                await model.SetRecordDirectoryAsync(folder);
                Assert.Equal(folder, await model.GetRecordDirectoryAsync());
                await record.StartAsync();
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => record.Status == ObsOutputStatus.Started, Wait), $"The recording did not start: it is {record.Status}.");

                var directory = await Assert.ThrowsAsync<ObsResponseException>(() => model.SetRecordDirectoryAsync(other));
                output.WriteLine($"The folder while it records: {directory.ErrorCode}: {directory.Message}");
                Assert.Equal(RequestStatusCode.OutputRunning, directory.ErrorCode);
                Assert.Equal(folder, await model.GetRecordDirectoryAsync());

                var video = await model.GetVideoSettingsAsync();
                var settings = await Assert.ThrowsAsync<ObsResponseException>(() => model.SetVideoSettingsAsync(frameRate: (24, 1)));
                output.WriteLine($"The video settings while it records: {settings.ErrorCode}: {settings.Message}");
                Assert.Equal(RequestStatusCode.OutputRunning, settings.ErrorCode);
                Assert.Equal(video.ToString(), (await model.GetVideoSettingsAsync()).ToString());

                // The stream service is only refused while OBS Studio streams.
                await model.SetStreamServiceAsync("rtmp_custom", new { server = "rtmp://127.0.0.1/obsclient-live", key = "not-a-key" });
                Assert.Equal("rtmp_custom", (await model.GetStreamServiceAsync()).Type);

                // Parameters are only written to the configuration, so OBS Studio takes them while it records.
                await model.Profiles.SetParameterAsync("obsclient-live", "probe", "1");
                await model.Profiles.SetParameterAsync("obsclient-live", "probe", null);

                await record.StopAsync();
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => record.Status == ObsOutputStatus.Stopped, Wait), $"The recording did not stop: it is {record.Status}.");
                for (var attempt = 0; attempt < 40 && (await client.GetRecordStatus()).OutputActive; attempt++)
                {
                    await Task.Delay(250);
                }

                await model.SetVideoSettingsAsync(frameRate: (24, 1));
                Assert.Equal(24, (await model.GetVideoSettingsAsync()).FpsNumerator);
            }
            finally
            {
                try
                {
                    await client.StopRecord();
                }
                catch (ObsResponseException)
                {
                    // It does not run: it was stopped by the test.
                }

                for (var attempt = 0; attempt < 40 && (await client.GetRecordStatus()).OutputActive; attempt++)
                {
                    await Task.Delay(250);
                }

                await RestoreAsync(client, original, made);
                await DeleteFolderAsync(folder);
                await DeleteFolderAsync(other);
            }
        }
    }
}
