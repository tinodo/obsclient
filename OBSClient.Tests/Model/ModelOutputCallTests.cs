namespace OBSStudioClient.Tests.Model
{
    using System.Text.Json;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Events;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Model;
    using OBSStudioClient.Tests.TestSupport;

    /// <summary>
    /// What the outputs of OBS Studio can be asked and told besides starting and stopping: captions, figures, splitting a recording and chapters, the file that an output wrote, the
    /// outputs that plugins add; and the events that say where a file was written.
    /// </summary>
    public class ModelOutputCallTests
    {
        private const int OutputEvents = 64;
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        [Fact]
        public async Task ACaptionOfTheStream_IsSentWithItsText()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("SendStreamCaption");

            await test.Model.Outputs.Stream.SendCaptionAsync("Hello, chat").WithTimeoutAsync(Timeout);
            await test.Model.Outputs.Stream.SendCaptionAsync(string.Empty).WithTimeoutAsync(Timeout);

            var sent = test.AllSent("SendStreamCaption");
            Assert.Equal("Hello, chat", sent[0].GetProperty("captionText").GetString());
            Assert.Equal(string.Empty, sent[1].GetProperty("captionText").GetString());
            Assert.Throws<ArgumentNullException>(() => { _ = test.Model.Outputs.Stream.SendCaptionAsync(null!); });
        }

        [Fact]
        public async Task ACaptionOfAStreamThatDoesNotRun_IsAResponseException()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("SendStreamCaption", 501, "The stream is not running.");

            var exception = await Assert.ThrowsAsync<ObsResponseException>(() => test.Model.Outputs.Stream.SendCaptionAsync("late").WithTimeoutAsync(Timeout));

            Assert.Equal(RequestStatusCode.OutputNotRunning, exception.ErrorCode);
        }

        [Fact]
        public async Task TheFiguresOfTheStreamAndOfTheRecording_AreAskedForWhenTheyAreWanted()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("GetStreamStatus", 100, new
            {
                outputActive = true,
                outputReconnecting = false,
                outputTimecode = "00:01:02.345",
                outputDuration = 62345,
                outputCongestion = 0.25,
                outputBytes = 1234567,
                outputSkippedFrames = 3,
                outputTotalFrames = 3700,
            });
            test.Answer("GetRecordStatus", 100, new { outputActive = true, outputPaused = false, outputTimecode = "00:00:10.000", outputDuration = 10000, outputBytes = 99 });

            var stream = await test.Model.Outputs.Stream.GetStatisticsAsync().WithTimeoutAsync(Timeout);
            var record = await test.Model.Outputs.Record.GetStatisticsAsync().WithTimeoutAsync(Timeout);

            Assert.True(stream.OutputActive);
            Assert.Equal("00:01:02.345", stream.OutputTimecode);
            Assert.Equal(62345, stream.OutputDuration);
            Assert.Equal(0.25f, stream.OutputCongestion);
            Assert.Equal(3, stream.OutputSkippedFrames);
            Assert.Equal(3700, stream.OutputTotalFrames);
            Assert.Equal(1234567, stream.OutputBytes);
            Assert.True(record.OutputActive);
            Assert.False(record.OutputPaused);
            Assert.Equal(10000, record.OutputDuration);
            Assert.Equal(99, record.OutputBytes);
        }

        [Fact]
        public async Task TheRecording_CanBeSplit_AndGetAChapter()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("SplitRecordFile");
            test.Answer("CreateRecordChapter");

            await test.Model.Outputs.Record.SplitFileAsync().WithTimeoutAsync(Timeout);
            await test.Model.Outputs.Record.CreateChapterAsync().WithTimeoutAsync(Timeout);
            await test.Model.Outputs.Record.CreateChapterAsync("The boss fight").WithTimeoutAsync(Timeout);

            Assert.Equal(1, test.Count("SplitRecordFile"));
            var chapters = test.AllSent("CreateRecordChapter");
            Assert.Equal(2, chapters.Length);
            Assert.False(chapters[0].ValueKind == JsonValueKind.Object && chapters[0].TryGetProperty("chapterName", out _));
            Assert.Equal("The boss fight", chapters[1].GetProperty("chapterName").GetString());
        }

        [Theory]
        [InlineData("SplitRecordFile", 501, RequestStatusCode.OutputNotRunning)]
        [InlineData("SplitRecordFile", 702, RequestStatusCode.RequestProcessingFailed)]
        [InlineData("CreateRecordChapter", 501, RequestStatusCode.OutputNotRunning)]
        [InlineData("CreateRecordChapter", 702, RequestStatusCode.RequestProcessingFailed)]
        public async Task ASplitOrAChapterThatOBSStudioRefuses_IsAResponseException(string request, int code, RequestStatusCode expected)
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer(request, code, "Verify that file splitting is enabled in the output settings.");

            var exception = await Assert.ThrowsAsync<ObsResponseException>(
                () => (request == "SplitRecordFile" ? test.Model.Outputs.Record.SplitFileAsync() : test.Model.Outputs.Record.CreateChapterAsync("x")).WithTimeoutAsync(Timeout));

            Assert.Equal(expected, exception.ErrorCode);
        }

        [Fact]
        public async Task ASplitThatTheObsWebSocketOfOBSStudioCannotDo_IsAResponseException_ThatNamesTheNeededVersion()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("SplitRecordFile", 204, "Unknown request type.");

            var exception = await Assert.ThrowsAsync<ObsResponseException>(() => test.Model.Outputs.Record.SplitFileAsync().WithTimeoutAsync(Timeout));

            Assert.Equal(RequestStatusCode.UnknownRequestType, exception.ErrorCode);
            Assert.Contains("splitting the recording file", exception.Message);
            Assert.Contains("WebSocketVersion", exception.Message);
        }

        [Fact]
        public async Task ThePauseOfTheRecording_CanBeToggled_AndTheCallSaysWhatTheModelShows()
        {
            await using var test = await CallFixture.StartAsync(setup: world => world.Outputs[ObsOutputKind.Record] = ObsOutputStatus.Started);
            var record = test.Model.Outputs.Record;
            var paused = true;
            test.Answer("ToggleRecordPause", request =>
            {
                var now = paused;
                _ = Task.Run(async () =>
                {
                    await Task.Delay(50);
                    await test.Rig.Responder.SendEventAsync("RecordStateChanged", new { outputActive = true, outputState = now ? "OBS_WEBSOCKET_OUTPUT_PAUSED" : "OBS_WEBSOCKET_OUTPUT_RESUMED", outputPath = (string?)null }, OutputEvents);
                });
                paused = !paused;
                return (100, new { outputPaused = now });
            });

            var first = await record.TogglePauseAsync().WithTimeoutAsync(Timeout);
            Assert.True(first);
            Assert.True(record.IsPaused);

            var second = await record.TogglePauseAsync().WithTimeoutAsync(Timeout);
            Assert.False(second);
            Assert.False(record.IsPaused);
            Assert.Equal(ObsOutputStatus.Started, record.Status);
        }

        [Fact]
        public async Task ThePathOfTheLastReplay_IsAskedFor_AndAReplayBufferThatDoesNotRunRefusesIt()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("GetLastReplayBufferReplay", 100, new { savedReplayPath = "C:\\Videos\\Replay 1.mkv" });

            var path = await test.Model.Outputs.ReplayBuffer.GetLastReplayFileAsync().WithTimeoutAsync(Timeout);

            Assert.Equal("C:\\Videos\\Replay 1.mkv", path);

            test.Answer("GetLastReplayBufferReplay", 501, "The replay buffer is not active.");
            var exception = await Assert.ThrowsAsync<ObsResponseException>(() => test.Model.Outputs.ReplayBuffer.GetLastReplayFileAsync().WithTimeoutAsync(Timeout));
            Assert.Equal(RequestStatusCode.OutputNotRunning, exception.ErrorCode);
        }

        [Fact]
        public async Task AReplayThatWasSaved_IsToldInASavedEvent_AndIsTheLastFileOfTheReplayBuffer()
        {
            await using var test = await CallFixture.StartAsync();
            var buffer = test.Model.Outputs.ReplayBuffer;
            var told = new List<string>();
            var senders = new List<object?>();
            buffer.Saved += (sender, e) =>
            {
                lock (told)
                {
                    senders.Add(sender);
                    told.Add($"saved:{e.SavedReplayPath}");
                }
            };
            buffer.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ObsReplayBufferOutput.LastSavedPath))
                {
                    lock (told)
                    {
                        told.Add("LastSavedPath");
                    }
                }
            };
            Assert.Null(buffer.LastSavedPath);

            // One after the other: what is told of two that arrive together can be told in one step.
            await test.Rig.Responder.SendEventAsync("ReplayBufferSaved", new { savedReplayPath = "C:\\Videos\\one.mkv" }, OutputEvents);
            await ModelRig.EventuallyAsync(() => Told(told) == 2, "the first replay to be told");
            await test.Rig.Responder.SendEventAsync("ReplayBufferSaved", new { savedReplayPath = "C:\\Videos\\two.mkv" }, OutputEvents);
            await ModelRig.EventuallyAsync(() => Told(told) == 4, "the second replay to be told");

            // The property is told before the event that says that the replay was saved, so that a handler of the event finds the file.
            lock (told)
            {
                Assert.Equal(new[] { "LastSavedPath", "saved:C:\\Videos\\one.mkv", "LastSavedPath", "saved:C:\\Videos\\two.mkv" }, told);
                Assert.All(senders, sender => Assert.Same(buffer, sender));
            }

            Assert.Equal("C:\\Videos\\two.mkv", buffer.LastSavedPath);
            var changes = test.Rig.Changes.Where(change => ReferenceEquals(change.Entity, buffer) && change.PropertyName == "LastSavedPath").ToArray();
            Assert.Equal(2, changes.Length);
            Assert.Null(changes[0].OldValue);
            Assert.Equal("C:\\Videos\\one.mkv", changes[0].NewValue);
            Assert.Equal("C:\\Videos\\one.mkv", changes[1].OldValue);
            Assert.Equal("C:\\Videos\\two.mkv", changes[1].NewValue);
        }

        [Fact]
        public async Task ARecordingThatContinuesInAnotherFile_IsToldInAFileChangedEvent_AndTheNewFileIsTheCurrentFile()
        {
            await using var test = await CallFixture.StartAsync();
            var record = test.Model.Outputs.Record;
            var told = new List<string>();
            var senders = new List<object?>();
            record.FileChanged += (sender, e) =>
            {
                lock (told)
                {
                    senders.Add(sender);
                    told.Add($"changed:{e.NewOutputPath}");
                }
            };
            record.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ObsRecordOutput.CurrentFile))
                {
                    lock (told)
                    {
                        told.Add("CurrentFile");
                    }
                }
            };
            Assert.Null(record.CurrentFile);

            await test.Rig.Responder.SendEventAsync("RecordFileChanged", new { newOutputPath = "C:/Videos/part2.mkv" }, OutputEvents);
            await ModelRig.EventuallyAsync(() => Told(told) == 2, "the new file to be told");

            // The property is told before the event that says that the recording went on in a new file, so that a handler of the event finds the file.
            lock (told)
            {
                Assert.Equal(new[] { "CurrentFile", "changed:C:/Videos/part2.mkv" }, told);
            }

            Assert.Same(record, Assert.Single(senders));
            Assert.Equal("C:/Videos/part2.mkv", record.CurrentFile);
            Assert.Equal(ObsOutputStatus.Stopped, record.Status);
        }
        [Fact]
        public async Task TheFileOfARecording_IsSetWhenItStartsAndWhenItStops_AndAHandlerOfTheStatusFindsIt()
        {
            await using var test = await CallFixture.StartAsync();
            var record = test.Model.Outputs.Record;
            var seen = new List<string>();
            record.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ObsOutput.Status))
                {
                    lock (seen)
                    {
                        seen.Add($"{record.Status}:{record.CurrentFile}");
                    }
                }
            };

            await test.Rig.Responder.SendEventAsync("RecordStateChanged", new { outputActive = true, outputState = "OBS_WEBSOCKET_OUTPUT_STARTED", outputPath = "C:/Videos/a.mkv" }, OutputEvents);
            await ModelRig.EventuallyAsync(() => Told(seen) == 1, "the start to be told");
            await test.Rig.Responder.SendEventAsync("RecordStateChanged", new { outputActive = false, outputState = "OBS_WEBSOCKET_OUTPUT_STOPPED", outputPath = "C:/Videos/a.mkv" }, OutputEvents);
            await ModelRig.EventuallyAsync(() => Told(seen) == 2, "the stop to be told");

            Assert.Equal(new[] { "Started:C:/Videos/a.mkv", "Stopped:C:/Videos/a.mkv" }, seen);
            Assert.Equal("C:/Videos/a.mkv", record.CurrentFile);
        }
        private static int Told<T>(List<T> list)
        {
            lock (list)
            {
                return list.Count;
            }
        }
        [Fact]
        public async Task TheOutputsThatOBSStudioHas_AreListed_WithTheirNamesAndKinds()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("GetOutputList", 100, new
            {
                outputs = new[]
                {
                    new { outputActive = false, outputFlags = new { OBS_OUTPUT_VIDEO = true, OBS_OUTPUT_AUDIO = true }, outputHeight = 1080, outputKind = "rtmp_output", outputName = "adv_stream", outputWidth = 1920 },
                    new { outputActive = true, outputFlags = new { OBS_OUTPUT_VIDEO = true, OBS_OUTPUT_AUDIO = false }, outputHeight = 720, outputKind = "virtualcam_output", outputName = "virtualcam_output", outputWidth = 1280 },
                },
            });

            var outputs = await test.Model.Outputs.GetAllAsync().WithTimeoutAsync(Timeout);

            Assert.Equal(new[] { "adv_stream", "virtualcam_output" }, outputs.Select(output => output.OutputName).ToArray());
            Assert.Equal("rtmp_output", outputs[0].OutputKind);
            Assert.False(outputs[0].OutputActive);
            Assert.True(outputs[1].OutputActive);
            Assert.True(outputs[0].OutputFlags["OBS_OUTPUT_AUDIO"]);
            Assert.Equal(1280, outputs[1].OutputWidth);

            // The four that the model keeps current are still four.
            Assert.Equal(4, test.Model.Outputs.Count);
        }

        [Fact]
        public async Task AnOutputByItsName_IsStartedStoppedAndToggled_WithoutTheModelKeepingItsState()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("StartOutput");
            test.Answer("StopOutput");
            test.Answer("ToggleOutput", 100, new { outputActive = true });
            var output = test.Model.Outputs.Named("Second stream");

            await output.StartAsync().WithTimeoutAsync(Timeout);
            await output.StopAsync().WithTimeoutAsync(Timeout);
            var started = await output.ToggleAsync().WithTimeoutAsync(Timeout);

            Assert.Equal("Second stream", output.Name);
            Assert.Same(test.Model, output.Model);
            Assert.True(started);
            foreach (var type in new[] { "StartOutput", "StopOutput", "ToggleOutput" })
            {
                var sent = Assert.Single(test.AllSent(type));
                Assert.Equal("Second stream", sent.GetProperty("outputName").GetString());
            }

            Assert.Equal("Output 'Second stream'", output.ToString());
            Assert.Equal(ObsOutputStatus.Stopped, test.Model.Outputs.Stream.Status);
        }

        [Fact]
        public async Task TheStatusOfAnOutputByItsName_IsAskedFor()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("GetOutputStatus", 100, new
            {
                outputActive = true,
                outputReconnecting = true,
                outputTimecode = "00:00:05.000",
                outputDuration = 5000,
                outputCongestion = 0.5,
                outputBytes = 42,
                outputSkippedFrames = 1,
                outputTotalFrames = 300,
            });

            var status = await test.Model.Outputs.Named("Second stream").GetStatusAsync().WithTimeoutAsync(Timeout);

            Assert.True(status.OutputActive);
            Assert.True(status.OutputReconnecting);
            Assert.Equal(5000, status.OutputDuration);
            Assert.Equal(300, status.OutputTotalFrames);
            Assert.Equal("Second stream", Assert.Single(test.AllSent("GetOutputStatus")).GetProperty("outputName").GetString());
        }

        [Fact]
        public async Task TheSettingsOfAnOutputByItsName_AreReadAsJson_AndSetFromAnObject()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("GetOutputSettings", 100, new { outputSettings = new { max_size_mb = 100, path = "C:\\Videos\\x.mkv", nested = new { on = true } } });
            test.Answer("SetOutputSettings");
            var output = test.Model.Outputs.Named("Replay");

            var settings = await output.GetSettingsAsync().WithTimeoutAsync(Timeout);
            await output.SetSettingsAsync(new { max_size_mb = 200 }).WithTimeoutAsync(Timeout);
            using var document = JsonDocument.Parse("""{"path":"D:\\Other"}""");
            await output.SetSettingsAsync(document.RootElement).WithTimeoutAsync(Timeout);

            Assert.Equal(JsonValueKind.Object, settings.ValueKind);
            Assert.Equal(100, settings.GetProperty("max_size_mb").GetInt32());
            Assert.Equal("C:\\Videos\\x.mkv", settings.GetProperty("path").GetString());
            Assert.True(settings.GetProperty("nested").GetProperty("on").GetBoolean());
            var sent = test.AllSent("SetOutputSettings");
            Assert.Equal("Replay", sent[0].GetProperty("outputName").GetString());
            Assert.Equal(200, sent[0].GetProperty("outputSettings").GetProperty("max_size_mb").GetInt32());
            Assert.Equal("D:\\Other", sent[1].GetProperty("outputSettings").GetProperty("path").GetString());
        }

        [Fact]
        public async Task TheSettingsOfAnOutput_AreRefusedBeforeAnythingIsSent_WhenTheyAreNoObject()
        {
            await using var test = await CallFixture.StartAsync();
            var output = test.Model.Outputs.Named("Replay");

            Assert.Throws<ArgumentNullException>(() => { _ = output.SetSettingsAsync(null!); });
            Assert.Equal("settings", Assert.Throws<ArgumentException>(() => { _ = output.SetSettingsAsync(5); }).ParamName);
            Assert.Equal("settings", Assert.Throws<ArgumentException>(() => { _ = output.SetSettingsAsync("text"); }).ParamName);
            Assert.Contains("CancellationToken", Assert.Throws<ArgumentException>(() => { _ = output.SetSettingsAsync(CancellationToken.None); }).Message);
            Assert.Throws<ArgumentException>(() => test.Model.Outputs.Named(string.Empty));
            Assert.Equal(0, test.Count("SetOutputSettings"));
        }

        [Fact]
        public async Task AnOutputThatDoesNotExist_IsAResponseException_AndNotARemoval()
        {
            await using var test = await CallFixture.StartAsync();
            foreach (var type in new[] { "StartOutput", "StopOutput", "ToggleOutput", "GetOutputStatus", "GetOutputSettings", "SetOutputSettings" })
            {
                test.Answer(type, 600, "No output was found with the name `Nope`.");
            }

            var output = test.Model.Outputs.Named("Nope");
            var exceptions = new[]
            {
                await Assert.ThrowsAsync<ObsResponseException>(() => output.StartAsync().WithTimeoutAsync(Timeout)),
                await Assert.ThrowsAsync<ObsResponseException>(() => output.StopAsync().WithTimeoutAsync(Timeout)),
                await Assert.ThrowsAsync<ObsResponseException>(() => output.ToggleAsync().WithTimeoutAsync(Timeout)),
                await Assert.ThrowsAsync<ObsResponseException>(() => output.GetStatusAsync().WithTimeoutAsync(Timeout)),
                await Assert.ThrowsAsync<ObsResponseException>(() => output.GetSettingsAsync().WithTimeoutAsync(Timeout)),
                await Assert.ThrowsAsync<ObsResponseException>(() => output.SetSettingsAsync(new { a = 1 }).WithTimeoutAsync(Timeout)),
            };

            Assert.All(exceptions, exception => Assert.Equal(RequestStatusCode.ResourceNotFound, exception.ErrorCode));
            Assert.Contains("No output was found", exceptions[0].Message);
        }

        [Fact]
        public async Task AnOutputThatRunsAlready_OrThatDoesNotRun_IsAResponseException()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("StartOutput", 500, "The output is running.");
            test.Answer("StopOutput", 501, "The output is not running.");
            var output = test.Model.Outputs.Named("Second stream");

            var start = await Assert.ThrowsAsync<ObsResponseException>(() => output.StartAsync().WithTimeoutAsync(Timeout));
            var stop = await Assert.ThrowsAsync<ObsResponseException>(() => output.StopAsync().WithTimeoutAsync(Timeout));

            Assert.Equal(RequestStatusCode.OutputRunning, start.ErrorCode);
            Assert.Equal(RequestStatusCode.OutputNotRunning, stop.ErrorCode);
        }
    }
}
