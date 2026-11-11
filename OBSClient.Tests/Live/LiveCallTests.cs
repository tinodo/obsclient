namespace OBSStudioClient.Tests.Live
{
    using System.Text.Json;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Model;
    using OBSStudioClient.Tests.TestSupport;
    using Xunit.Abstractions;

    /// <summary>
    /// The calls of the model that ask OBS Studio something or tell it something and change nothing that the model shows, against a real OBS Studio: they check what the model assumes of it
    /// where the stand-in of the unit tests only had what was read in the source of obs-websocket (the words of the refusals, what a hotkey context is, what a screenshot looks like).
    /// Everything that is made carries the name prefix of <see cref="LiveObs.NamePrefix"/> and is removed again. No window of OBS Studio is opened.
    /// </summary>
    [Collection("Live OBS")]
    public class LiveCallTests(ITestOutputHelper output)
    {
        private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        private static readonly TimeSpan Wait = TimeSpan.FromSeconds(20);

        private static object ColorSettings => new { color = 4278190335L, width = 640, height = 360 };

        [LiveUuidChangesFact]
        public async Task TheModel_KnowsTheVersionAndThePlatformOfOBSStudio_AndTheImageFormatsOfItsScreenshots()
        {
            await using var live = await LiveModel.ConnectAsync(output);
            var model = live.Model;

            output.WriteLine($"OBS Studio {model.ObsVersion}, obs-websocket {model.WebSocketVersion}, platform {model.Platform}, image formats {string.Join(", ", model.SupportedImageFormats)}");

            Assert.Equal(live.ObsVersion, model.ObsVersion);
            Assert.Equal(live.WebSocketVersion, model.WebSocketVersion);
            Assert.False(string.IsNullOrEmpty(model.Platform));
            Assert.Contains("png", model.SupportedImageFormats);
            Assert.Contains("jpg", model.SupportedImageFormats);
        }

        [LiveUuidChangesFact]
        public async Task TheStatistics_TheHotkeys_AndTheMonitors_AreAskedFor()
        {
            await using var live = await LiveModel.ConnectAsync(output);
            var model = live.Model;

            var stats = await model.GetStatsAsync();
            var hotkeys = await model.GetHotkeysAsync();
            var monitors = await model.GetMonitorsAsync();

            output.WriteLine($"cpu {stats.CpuUsage:0.0} %, memory {stats.MemoryUsage:0} MB, {stats.ActiveFps:0.0} fps, {stats.RenderTotalFrames} frames rendered, {hotkeys.Count} hotkeys, {monitors.Count} monitors");
            Assert.True(stats.CpuUsage >= 0);
            Assert.True(stats.MemoryUsage > 0);
            Assert.True(stats.ActiveFps > 0);
            Assert.True(stats.WebSocketSessionIncomingMessages > 0);
            Assert.NotEmpty(hotkeys);
            Assert.All(hotkeys, hotkey => Assert.False(string.IsNullOrEmpty(hotkey)));
            Assert.All(monitors, monitor => Assert.True(monitor.MonitorWidth > 0 && monitor.MonitorHeight > 0));
        }

        [LiveUuidChangesFact]
        public async Task AScreenshot_OfASceneAndOfAnInput_IsAPicture_AndCanBeSavedByOBSStudio()
        {
            await using var live = await LiveModel.ConnectAsync(output);
            var model = live.Model;
            var scene = await model.CreateSceneAsync(LiveObs.UniqueName("shot"));
            var input = await scene.CreateInputAsync(LiveObs.UniqueName("color"), await live.ColorKindAsync(), ColorSettings);
            var folder = Path.Combine(Path.GetTempPath(), "obsclient-live-" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(folder);
            try
            {
                var png = await scene.GetScreenshotAsync("png", 160, 90);
                var jpg = await input.GetScreenshotAsync("jpg", quality: 50);
                var plain = await input.GetScreenshotAsync();

                output.WriteLine($"png of the scene: {png.Data.Length} bytes; jpg of the input: {jpg.Data.Length} bytes; the input as it is: {plain.Data.Length} bytes");
                Assert.True(png.Data.Span.StartsWith(PngSignature), "The screenshot of the scene is no PNG.");
                Assert.True(jpg.Data.Span.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }), "The screenshot of the input is no JPEG.");
                Assert.True(plain.Data.Span.StartsWith(PngSignature));
                Assert.StartsWith("data:image/png;base64,", png.ToDataUri());

                var path = Path.Combine(folder, "scene.png");
                await scene.SaveScreenshotAsync(path, "png", 160, 90);
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => File.Exists(path) && new FileInfo(path).Length > 0, Wait), "OBS Studio wrote no file.");
                Assert.True((await File.ReadAllBytesAsync(path)).AsSpan().StartsWith(PngSignature));
            }
            finally
            {
                Directory.Delete(folder, recursive: true);
            }
        }

        [LiveUuidChangesFact]
        public async Task AScreenshotToAFolderThatDoesNotExist_IsAResponseException_NotARemoval()
        {
            await using var live = await LiveModel.ConnectAsync(output);
            var scene = await live.Model.CreateSceneAsync(LiveObs.UniqueName("shot"));
            var path = Path.Combine(Path.GetTempPath(), "obsclient-live-nowhere-" + Guid.NewGuid().ToString("N")[..8], "scene.png");

            var refused = await Assert.ThrowsAsync<ObsResponseException>(() => scene.SaveScreenshotAsync(path));

            output.WriteLine($"{refused.ErrorCode}: {refused.Message}");
            Assert.Equal(RequestStatusCode.ResourceNotFound, refused.ErrorCode);
            Assert.False(scene.IsRemoved);
            Assert.False(File.Exists(path));
        }

        [LiveUuidChangesFact]
        public async Task AFormatThatOBSStudioDoesNotKnow_IsAResponseException()
        {
            await using var live = await LiveModel.ConnectAsync(output);
            var scene = await live.Model.CreateSceneAsync(LiveObs.UniqueName("shot"));

            var refused = await Assert.ThrowsAsync<ObsResponseException>(() => scene.GetScreenshotAsync("not-a-format"));

            output.WriteLine($"{refused.ErrorCode}: {refused.Message}");
            Assert.Equal(RequestStatusCode.InvalidRequestField, refused.ErrorCode);
        }

        [LiveUuidChangesFact]
        public async Task WhetherASourceIsOnTheAir_IsWhatOBSStudioSays()
        {
            await using var live = await LiveModel.ConnectAsync(output);
            var model = live.Model;
            var idle = await model.CreateSceneAsync(LiveObs.UniqueName("idle"));
            Assert.NotNull(model.ProgramScene);

            var program = await model.ProgramScene!.GetActivityAsync();
            var notProgram = await idle.GetActivityAsync();

            output.WriteLine($"program scene: active {program.VideoActive}, showing {program.VideoShowing}; the other: active {notProgram.VideoActive}, showing {notProgram.VideoShowing}");
            Assert.True(program.VideoActive);
            Assert.False(notProgram.VideoActive);
        }

        [LiveUuidChangesFact]
        public async Task AHotkey_IsTriggeredByItsName_AndTheHotkeyOfASourceByTheNameOfTheSource()
        {
            await using var live = await LiveModel.ConnectAsync(output);
            var model = live.Model;
            var scene = await model.CreateSceneAsync(LiveObs.UniqueName("keys"));
            var input = await scene.CreateInputAsync(LiveObs.UniqueName("media"), "ffmpeg_source", new { is_local_file = true });
            Assert.True(input.HasAudio);
            Assert.False(input.IsMuted);

            var refused = await Assert.ThrowsAsync<ObsResponseException>(() => model.TriggerHotkeyAsync("obsclient.live.nobody"));
            output.WriteLine($"{refused.ErrorCode}: {refused.Message}");
            Assert.Equal(RequestStatusCode.ResourceNotFound, refused.ErrorCode);

            await input.TriggerHotkeyAsync("libobs.mute");
            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => input.IsMuted, Wait), "The hotkey did not mute the input.");
            await model.TriggerHotkeyAsync("libobs.unmute", input.Name);
            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => !input.IsMuted, Wait), "The hotkey did not unmute the input.");

            var refusedOfTheSource = await Assert.ThrowsAsync<ObsResponseException>(() => input.TriggerHotkeyAsync("obsclient.live.nobody"));
            Assert.Equal(RequestStatusCode.ResourceNotFound, refusedOfTheSource.ErrorCode);
            Assert.False(input.IsRemoved);
            await live.AssertModelEqualsObsAsync("after the hotkeys");
        }

        [LiveUuidChangesFact]
        public async Task AKeySequenceWithoutAKeyAndModifiers_IsRefusedByOBSStudio()
        {
            await using var live = await LiveModel.ConnectAsync(output);

            var refused = await Assert.ThrowsAsync<ObsResponseException>(() => live.Model.TriggerHotkeyAsync(ObsKey.OBS_KEY_NONE));

            output.WriteLine($"{refused.ErrorCode}: {refused.Message}");
            Assert.Equal(RequestStatusCode.CannotAct, refused.ErrorCode);
        }

        [LiveUuidChangesFact]
        public async Task ACustomEvent_ComesBackToTheModel_AsOBSStudioSendsItToEveryClient()
        {
            await using var live = await LiveModel.ConnectAsync(output);
            var model = live.Model;
            var received = new List<CustomEventData>();
            model.CustomEvent += (_, e) =>
            {
                lock (received)
                {
                    received.Add(new CustomEventData(e.EventData!.Value.GetProperty("source").GetString()!, e.EventData!.Value.GetProperty("n").GetInt32()));
                }
            };

            await model.BroadcastCustomEventAsync(new { source = "the live tests", n = 1 });
            await model.BroadcastCustomEventAsync(new { source = "the live tests", n = 2 });

            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => { lock (received) { return received.Count == 2; } }, Wait), "The custom events did not come back.");
            lock (received)
            {
                Assert.Equal(new[] { 1, 2 }, received.Select(data => data.N).ToArray());
                Assert.All(received, data => Assert.Equal("the live tests", data.Source));
            }

            var refused = await Assert.ThrowsAsync<ObsResponseException>(() => model.BroadcastCustomEventAsync(new { }));
            output.WriteLine($"An empty custom event: {refused.ErrorCode}: {refused.Message}");
        }

        [LiveUuidChangesFact]
        public async Task AVendorRequestThatDoesNotExist_IsAResponseException_NotARemoval()
        {
            await using var live = await LiveModel.ConnectAsync(output);

            var refused = await Assert.ThrowsAsync<ObsResponseException>(() => live.Model.CallVendorRequestAsync("obsclient-live-nobody", "NoRequest", new { a = 1 }));

            output.WriteLine($"{refused.ErrorCode}: {refused.Message}");
            Assert.Equal(RequestStatusCode.ResourceNotFound, refused.ErrorCode);
        }

        [LiveUuidChangesFact]
        public async Task TheInteractionWindowOfAnInputThatCannotBeInteractedWith_IsRefused_WithoutOpeningAWindow()
        {
            await using var live = await LiveModel.ConnectAsync(output);
            var scene = await live.Model.CreateSceneAsync(LiveObs.UniqueName("window"));
            var input = await scene.CreateInputAsync(LiveObs.UniqueName("color"), await live.ColorKindAsync(), ColorSettings);

            var refused = await Assert.ThrowsAsync<ObsResponseException>(() => input.OpenInteractDialogAsync());

            output.WriteLine($"{refused.ErrorCode}: {refused.Message}");
            Assert.Equal(RequestStatusCode.InvalidResourceState, refused.ErrorCode);
        }

        [LiveUuidChangesFact]
        public async Task TheOutputsOfOBSStudio_AreListed_AndAskedAboutByTheirNames()
        {
            await using var live = await LiveModel.ConnectAsync(output);
            var outputs = live.Model.Outputs;

            var all = await outputs.GetAllAsync();
            foreach (var entry in all)
            {
                output.WriteLine($"{entry.OutputName} ({entry.OutputKind}) active {entry.OutputActive} {entry.OutputWidth}x{entry.OutputHeight}");
            }

            Assert.All(all, entry => Assert.False(string.IsNullOrEmpty(entry.OutputName)));
            foreach (var entry in all.Take(3))
            {
                var named = outputs.Named(entry.OutputName);
                var status = await named.GetStatusAsync();
                var settings = await named.GetSettingsAsync();
                Assert.False(status.OutputActive);
                Assert.Equal(JsonValueKind.Object, settings.ValueKind);
            }

            var missing = outputs.Named("obsclient-live-no-such-output");
            var statusRefused = await Assert.ThrowsAsync<ObsResponseException>(() => missing.GetStatusAsync());
            var startRefused = await Assert.ThrowsAsync<ObsResponseException>(() => missing.StartAsync());
            output.WriteLine($"{statusRefused.ErrorCode}: {statusRefused.Message}");
            Assert.Equal(RequestStatusCode.ResourceNotFound, statusRefused.ErrorCode);
            Assert.Equal(RequestStatusCode.ResourceNotFound, startRefused.ErrorCode);

            var stream = await outputs.Stream.GetStatisticsAsync();
            var record = await outputs.Record.GetStatisticsAsync();
            Assert.False(stream.OutputActive);
            Assert.False(record.OutputActive);
            Assert.Equal(0, stream.OutputTotalFrames);
        }

        [LiveUuidChangesFact]
        public async Task TheReplayBufferThatDoesNotRun_RefusesTheLastReplayAndASave()
        {
            await using var live = await LiveModel.ConnectAsync(output);
            var buffer = live.Model.Outputs.ReplayBuffer;
            Assert.False(buffer.IsActive);

            var last = await Assert.ThrowsAsync<ObsResponseException>(() => buffer.GetLastReplayFileAsync());
            var save = await Assert.ThrowsAsync<ObsResponseException>(() => buffer.SaveAsync());

            output.WriteLine($"GetLastReplayBufferReplay: {last.ErrorCode}: {last.Message}");
            output.WriteLine($"SaveReplayBuffer: {save.ErrorCode}: {save.Message}");
            Assert.Null(buffer.LastSavedPath);
        }

        [LiveUuidChangesFact]
        public async Task ACaptionOfAStreamThatDoesNotRun_AndASplitOfARecordingThatDoesNotRun_AreRefused()
        {
            await using var live = await LiveModel.ConnectAsync(output);
            var outputs = live.Model.Outputs;

            var caption = await Assert.ThrowsAsync<ObsResponseException>(() => outputs.Stream.SendCaptionAsync("nobody listens"));
            var split = await Assert.ThrowsAsync<ObsResponseException>(() => outputs.Record.SplitFileAsync());
            var chapter = await Assert.ThrowsAsync<ObsResponseException>(() => outputs.Record.CreateChapterAsync("nobody records"));

            output.WriteLine($"caption: {caption.ErrorCode}: {caption.Message}");
            output.WriteLine($"split: {split.ErrorCode}: {split.Message}");
            output.WriteLine($"chapter: {chapter.ErrorCode}: {chapter.Message}");
            Assert.Equal(RequestStatusCode.OutputNotRunning, caption.ErrorCode);
            Assert.Equal(RequestStatusCode.OutputNotRunning, split.ErrorCode);
            Assert.Equal(RequestStatusCode.OutputNotRunning, chapter.ErrorCode);
        }

        [LiveUuidChangesFact]
        public async Task TheRecording_SaysTheFileItWrote_TakesAChapter_AndRefusesASplitThatIsNotEnabled()
        {
            await using var live = await LiveModel.ConnectAsync(output);
            var model = live.Model;
            var record = model.Outputs.Record;
            Assert.False(record.IsActive, "The OBS Studio of the live tests is recording already.");
            var changed = new List<string>();
            record.FileChanged += (_, e) =>
            {
                lock (changed)
                {
                    changed.Add(e.NewOutputPath);
                }
            };
            model.Client.RecordFileChanged += (_, e) => output.WriteLine($"RecordFileChanged: {e.NewOutputPath}");
            model.Client.RecordStateChanged += (_, e) => output.WriteLine($"RecordStateChanged: {e.OutputState} {e.OutputPath}");

            // The recording goes to a folder that this test makes and removes: the profile of a fresh OBS Studio has the videos folder of the user.
            var folder = Path.Combine(Path.GetTempPath(), "obsclient-live-" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(folder);
            var before = (await live.Client.GetProfileParameter("SimpleOutput", "FilePath")).ParameterValue;
            try
            {
                await live.Client.SetProfileParameter("SimpleOutput", "FilePath", folder);

                // Nothing is recorded before the folder is the one of this test: a recording that went to the videos folder of the user would stay there.
                Assert.Equal(folder, (await live.Client.GetProfileParameter("SimpleOutput", "FilePath")).ParameterValue);
                await record.StartAsync();
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => record.Status == ObsOutputStatus.Started, Wait), $"The recording did not start: it is {record.Status}.");
                await Task.Delay(1500);
                output.WriteLine($"The recording runs; the model knows the file: {record.CurrentFile ?? "(not yet)"}; the files that OBS Studio announced as a new one: {string.Join(", ", changed)}");

                // obs-websocket 5.5.0 and newer say the file of a recording with the event of its start, and send no RecordFileChanged for the first file.
                if (model.WebSocketVersion >= new Version(5, 5, 0))
                {
                    Assert.NotNull(record.CurrentFile);
                    Assert.StartsWith(folder, Path.GetFullPath(record.CurrentFile!), StringComparison.OrdinalIgnoreCase);
                    Assert.Empty(changed);
                }

                var figures = await record.GetStatisticsAsync();
                output.WriteLine($"{figures.OutputTimecode}, {figures.OutputDuration} ms, {figures.OutputBytes} bytes");
                Assert.True(figures.OutputActive);
                Assert.True(figures.OutputDuration > 0);

                // The default recording format of OBS Studio is Hybrid MP4, which has chapters. Another format refuses: both are acceptable, and the refusal is a response exception.
                try
                {
                    await record.CreateChapterAsync("the live test");
                    output.WriteLine("The chapter was added.");
                }
                catch (ObsResponseException refused)
                {
                    output.WriteLine($"The chapter was refused: {refused.ErrorCode}: {refused.Message}");
                    Assert.Equal(RequestStatusCode.RequestProcessingFailed, refused.ErrorCode);
                }

                // Only the advanced output has file splitting, and it is not enabled in this OBS Studio.
                var split = await Assert.ThrowsAsync<ObsResponseException>(() => record.SplitFileAsync());
                output.WriteLine($"The split was refused: {split.ErrorCode}: {split.Message}");
                Assert.Equal(RequestStatusCode.RequestProcessingFailed, split.ErrorCode);
                Assert.True(record.IsActive);

                await record.StopAsync();
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => record.Status == ObsOutputStatus.Stopped, Wait), $"The recording did not stop: it is {record.Status}.");
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => record.CurrentFile is not null, Wait), "The model was not told which file OBS Studio wrote.");
                output.WriteLine($"OBS Studio wrote {record.CurrentFile}");
                Assert.StartsWith(folder, Path.GetFullPath(record.CurrentFile!), StringComparison.OrdinalIgnoreCase);
                Assert.True(File.Exists(record.CurrentFile), "The file that OBS Studio announced is not there.");
            }
            finally
            {
                try
                {
                    await live.Client.StopRecord();
                }
                catch (ObsResponseException)
                {
                    // It does not run: it was stopped by the test.
                }

                for (var attempt = 0; attempt < 40 && (await live.Client.GetRecordStatus()).OutputActive; attempt++)
                {
                    await Task.Delay(250);
                }

                await live.Client.SetProfileParameter("SimpleOutput", "FilePath", before);
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
        }
        [LiveUuidChangesFact]
        public async Task TheRecording_WhenItIsSplit_GoesOnInANewFile_AndTheModelFollowsIt()
        {
            await using var live = await LiveModel.ConnectAsync(output);
            var model = live.Model;
            var record = model.Outputs.Record;
            Assert.False(record.IsActive, "The OBS Studio of the live tests is recording already.");
            if (model.WebSocketVersion < new Version(5, 5, 0))
            {
                output.WriteLine("This obs-websocket has no SplitRecordFile.");
                return;
            }

            var changed = new List<string>();
            record.FileChanged += (_, e) =>
            {
                lock (changed)
                {
                    changed.Add(e.NewOutputPath);
                }
            };
            model.Client.RecordFileChanged += (_, e) => output.WriteLine($"RecordFileChanged: {e.NewOutputPath}");
            model.Client.RecordStateChanged += (_, e) => output.WriteLine($"RecordStateChanged: {e.OutputState} {e.OutputPath}");

            // File splitting is a setting of the advanced output only, and OBS Studio reads the output mode when a profile is loaded: the test makes a profile of its own, sets it up, loads it (by
            // going to the profile that was there and back) and removes it afterwards. The recording goes to a folder that this test makes and removes.
            var folder = Path.Combine(Path.GetTempPath(), "obsclient-live-" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(folder);
            var original = (await live.Client.GetProfileList()).CurrentProfileName;
            var profile = LiveObs.UniqueName("split");
            var created = false;
            try
            {
                // OBS Studio makes and switches profiles on its user interface thread, after it has answered: wait for each, or the settings go to the profile that was there.
                await live.Client.CreateProfile(profile);
                created = true;
                await WaitForProfileAsync(live, profile);
                await live.Client.SetProfileParameter("Output", "Mode", "Advanced");
                await live.Client.SetProfileParameter("AdvOut", "RecType", "Standard");
                await live.Client.SetProfileParameter("AdvOut", "RecFilePath", folder);
                await live.Client.SetProfileParameter("SimpleOutput", "FilePath", folder);
                await live.Client.SetProfileParameter("AdvOut", "RecSplitFile", "true");
                await live.Client.SetProfileParameter("AdvOut", "RecSplitFileType", "Manual");
                await live.Client.SetCurrentProfile(original);
                await WaitForProfileAsync(live, original);
                await live.Client.SetCurrentProfile(profile);
                await WaitForProfileAsync(live, profile);
                await Task.Delay(1000);

                // Nothing is recorded before the profile is the one that was made: a recording that went to the videos folder of the user would stay there.
                Assert.Equal("Advanced", (await live.Client.GetProfileParameter("Output", "Mode")).ParameterValue);
                Assert.Equal(folder, (await live.Client.GetProfileParameter("AdvOut", "RecFilePath")).ParameterValue);
                Assert.Equal(folder, (await live.Client.GetProfileParameter("SimpleOutput", "FilePath")).ParameterValue);

                await record.StartAsync();
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => record.Status == ObsOutputStatus.Started, Wait), $"The recording did not start: it is {record.Status}.");
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => record.CurrentFile is not null, Wait), "The model was not told the file of the recording.");
                var first = record.CurrentFile;
                await Task.Delay(1500);

                await record.SplitFileAsync();
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => { lock (changed) { return changed.Count == 1; } }, Wait), "OBS Studio did not say that the recording went on in a new file.");
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => record.CurrentFile != first, Wait), "The model still shows the first file.");
                output.WriteLine($"The first file: {first}; after the split: {record.CurrentFile}");
                lock (changed)
                {
                    Assert.Equal(record.CurrentFile, changed[0]);
                }

                await record.StopAsync();
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => record.Status == ObsOutputStatus.Stopped, Wait), $"The recording did not stop: it is {record.Status}.");
                Assert.True(Directory.EnumerateFiles(folder).Count() >= 2, "OBS Studio did not write two files.");

                // OBS Studio says the file that the recording started with when it stops, also after a split; the file that it wrote last is the one that the split announced, and that stays.
                await Task.Delay(500);
                lock (changed)
                {
                    Assert.Equal(changed[0], record.CurrentFile);
                }
                Assert.NotEqual(first, record.CurrentFile);
            }
            finally
            {
                try
                {
                    await live.Client.StopRecord();
                }
                catch (ObsResponseException)
                {
                    // It does not run: it was stopped by the test.
                }

                for (var attempt = 0; attempt < 40 && (await live.Client.GetRecordStatus()).OutputActive; attempt++)
                {
                    await Task.Delay(250);
                }

                if (created)
                {
                    await live.Client.SetCurrentProfile(original);
                    await WaitForProfileAsync(live, original);
                    await live.Client.RemoveProfile(profile);
                }

                for (var attempt = 0; attempt < 20 && Directory.Exists(folder); attempt++)
                {
                    try
                    {
                        Directory.Delete(folder, recursive: true);
                    }
                    catch (IOException)
                    {
                        // OBS Studio still has the files open for a moment.
                        await Task.Delay(250);
                    }
                }
            }
        }
        private static async Task WaitForProfileAsync(LiveModel live, string name)
        {
            var until = DateTime.UtcNow + Wait;
            while ((await live.Client.GetProfileList()).CurrentProfileName != name)
            {
                Assert.True(DateTime.UtcNow < until, $"OBS Studio did not switch to the profile '{name}'.");
                await Task.Delay(100);
            }
        }

        private sealed record CustomEventData(string Source, int N);
    }
}
