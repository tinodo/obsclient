namespace OBSStudioClient.Tests.Model
{
    using System.Text.Json;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Model;
    using OBSStudioClient.Tests.TestSupport;

    /// <summary>
    /// The calls of the model that ask OBS Studio something or tell it something and change nothing that the model shows (the statistics, the hotkeys, the screenshots, the dialogs, a custom event,
    /// a vendor request, the projectors): what they send, what they return, and what they do when OBS Studio refuses, does not answer, or when there is no connection.
    /// </summary>
    public class ModelCallTests
    {
        private const int InputEvents = 8;
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);


        [Fact]
        public async Task TheStatistics_AreAskedFor_AndReturnedAsOBSStudioSaidThem()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("GetStats", 100, new
            {
                cpuUsage = 1.5,
                memoryUsage = 210.25,
                availableDiskSpace = 51200.0,
                activeFps = 60.0,
                averageFrameRenderTime = 2.5,
                renderSkippedFrames = 1,
                renderTotalFrames = 1000,
                outputSkippedFrames = 2,
                outputTotalFrames = 900,
                webSocketSessionIncomingMessages = 10,
                webSocketSessionOutgoingMessages = 20,
            });

            var stats = await test.Model.GetStatsAsync().WithTimeoutAsync(Timeout);

            Assert.Equal(1.5f, stats.CpuUsage);
            Assert.Equal(60f, stats.ActiveFps);
            Assert.Equal(1000, stats.RenderTotalFrames);
            Assert.Equal(900, stats.OutputTotalFrames);
            Assert.Equal(20, stats.WebSocketSessionOutgoingMessages);
            Assert.Equal(1, test.Count("GetStats"));
        }

        [Fact]
        public async Task TheHotkeys_AreAskedFor_AndReturnedAsTheirNames()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("GetHotkeyList", 100, new { hotkeys = new[] { "ReplayBuffer.Save", "OBSBasic.StartRecording" } });

            var hotkeys = await test.Model.GetHotkeysAsync().WithTimeoutAsync(Timeout);

            Assert.Equal(new[] { "ReplayBuffer.Save", "OBSBasic.StartRecording" }, hotkeys);
        }

        [Fact]
        public async Task AHotkey_IsTriggeredByItsName_WithTheContextWhenThereIsOne()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("TriggerHotkeyByName");

            await test.Model.TriggerHotkeyAsync("ReplayBuffer.Save").WithTimeoutAsync(Timeout);
            await test.Model.TriggerHotkeyAsync("libobs.mute", "Mic").WithTimeoutAsync(Timeout);

            var sent = test.AllSent("TriggerHotkeyByName");
            Assert.Equal(2, sent.Length);
            Assert.Equal("ReplayBuffer.Save", sent[0].GetProperty("hotkeyName").GetString());
            Assert.False(sent[0].TryGetProperty("contextName", out _));
            Assert.Equal("libobs.mute", sent[1].GetProperty("hotkeyName").GetString());
            Assert.Equal("Mic", sent[1].GetProperty("contextName").GetString());
        }

        [Fact]
        public async Task AHotkeyThatOBSStudioDoesNotKnow_IsAResponseException_AndNotARemoval()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("TriggerHotkeyByName", 600, "No hotkeys were found by that name.");

            var exception = await Assert.ThrowsAsync<ObsResponseException>(() => test.Model.TriggerHotkeyAsync("Nope").WithTimeoutAsync(Timeout));

            Assert.Equal(RequestStatusCode.ResourceNotFound, exception.ErrorCode);
            Assert.Contains("No hotkeys were found", exception.Message);
        }

        [Fact]
        public async Task TheHotkeyOfASource_IsTriggeredWithTheNameOfTheSourceAsItsContext()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("TriggerHotkeyByName");

            await test.Mic.TriggerHotkeyAsync("libobs.mute").WithTimeoutAsync(Timeout);
            await test.Gaming.TriggerHotkeyAsync("libobs.show_scene_item.Camera").WithTimeoutAsync(Timeout);

            var sent = test.AllSent("TriggerHotkeyByName");
            Assert.Equal("Mic", sent[0].GetProperty("contextName").GetString());
            Assert.Equal("Gaming", sent[1].GetProperty("contextName").GetString());
        }

        [Fact]
        public async Task AKeySequence_IsPressedWithItsModifiers()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("TriggerHotkeyByKeySequence");

            await test.Model.TriggerHotkeyAsync(ObsKey.OBS_KEY_A, KeyModifier.Control | KeyModifier.Shift).WithTimeoutAsync(Timeout);
            await test.Model.TriggerHotkeyAsync(ObsKey.OBS_KEY_F5).WithTimeoutAsync(Timeout);

            var sent = test.AllSent("TriggerHotkeyByKeySequence");
            Assert.Equal("OBS_KEY_A", sent[0].GetProperty("keyId").GetString());
            var modifiers = sent[0].GetProperty("keyModifiers");
            Assert.True(modifiers.GetProperty("shift").GetBoolean());
            Assert.True(modifiers.GetProperty("control").GetBoolean());
            Assert.False(modifiers.GetProperty("alt").GetBoolean());
            Assert.False(modifiers.GetProperty("command").GetBoolean());
            Assert.Equal("OBS_KEY_F5", sent[1].GetProperty("keyId").GetString());
            Assert.False(sent[1].GetProperty("keyModifiers").GetProperty("shift").GetBoolean());
        }

        [Fact]
        public async Task AKeySequenceWithoutKeyAndModifiers_IsRefusedByOBSStudio_AsAResponseException()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("TriggerHotkeyByKeySequence", 703, "Both the key id and the modifiers are empty.");

            var exception = await Assert.ThrowsAsync<ObsResponseException>(() => test.Model.TriggerHotkeyAsync(ObsKey.OBS_KEY_NONE).WithTimeoutAsync(Timeout));

            Assert.Equal(RequestStatusCode.CannotAct, exception.ErrorCode);
        }

        [Fact]
        public async Task ACustomEvent_IsBroadcastWithTheObjectThatWasGiven()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("BroadcastCustomEvent");

            await test.Model.BroadcastCustomEventAsync(new { source = "a test", number = 3 }).WithTimeoutAsync(Timeout);
            using var document = JsonDocument.Parse("""{"nested":{"a":[1,2]}}""");
            await test.Model.BroadcastCustomEventAsync(document.RootElement).WithTimeoutAsync(Timeout);

            var sent = test.AllSent("BroadcastCustomEvent");
            var first = sent[0].GetProperty("eventData");
            Assert.Equal("a test", first.GetProperty("source").GetString());
            Assert.Equal(3, first.GetProperty("number").GetInt32());
            Assert.Equal(2, sent[1].GetProperty("eventData").GetProperty("nested").GetProperty("a")[1].GetInt32());
        }

        [Fact]
        public async Task ACustomEvent_WhoseDataIsNoObject_IsRefusedBeforeAnythingIsSent()
        {
            await using var test = await CallFixture.StartAsync();

            Assert.Throws<ArgumentNullException>(() => { _ = test.Model.BroadcastCustomEventAsync(null!); });
            var text = Assert.Throws<ArgumentException>(() => { _ = test.Model.BroadcastCustomEventAsync("just text"); });
            var list = Assert.Throws<ArgumentException>(() => { _ = test.Model.BroadcastCustomEventAsync(new[] { 1, 2 }); });
            var token = Assert.Throws<ArgumentException>(() => { _ = test.Model.BroadcastCustomEventAsync(CancellationToken.None); });

            Assert.Equal("data", text.ParamName);
            Assert.Equal("data", list.ParamName);
            Assert.Contains("CancellationToken", token.Message);
            Assert.Equal(0, test.Count("BroadcastCustomEvent"));
        }

        [Fact]
        public async Task AVendorRequest_IsCalledByItsVendorAndName_AndItsAnswerIsReturned()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("CallVendorRequest", 100, new { vendorName = "a-plugin", requestType = "DoIt", responseData = new { done = true } });

            var answer = await test.Model.CallVendorRequestAsync("a-plugin", "DoIt", new { amount = 5 }).WithTimeoutAsync(Timeout);
            await test.Model.CallVendorRequestAsync("a-plugin", "DoIt").WithTimeoutAsync(Timeout);

            Assert.Equal("a-plugin", answer.VendorName);
            Assert.Equal("DoIt", answer.RequestType);
            var sent = test.AllSent("CallVendorRequest");
            Assert.Equal("a-plugin", sent[0].GetProperty("vendorName").GetString());
            Assert.Equal("DoIt", sent[0].GetProperty("requestType").GetString());
            Assert.Equal(5, sent[0].GetProperty("requestData").GetProperty("amount").GetInt32());
            Assert.False(sent[1].TryGetProperty("requestData", out _));
        }

        [Fact]
        public async Task AVendorOrARequestThatOBSStudioDoesNotKnow_IsAResponseException_AndNotARemoval()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("CallVendorRequest", 600, "No vendor was found by that name.");

            var exception = await Assert.ThrowsAsync<ObsResponseException>(() => test.Model.CallVendorRequestAsync("nobody", "DoIt").WithTimeoutAsync(Timeout));

            Assert.Equal(RequestStatusCode.ResourceNotFound, exception.ErrorCode);
        }

        [Fact]
        public async Task TheMonitors_AreAskedFor_AndReturned()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("GetMonitorList", 100, new
            {
                monitors = new[]
                {
                    new { monitorHeight = 1080, monitorIndex = 0, monitorName = "DELL(0)", monitorPositionX = 0, monitorPositionY = 0, monitorWidth = 1920 },
                    new { monitorHeight = 1440, monitorIndex = 1, monitorName = "LG(1)", monitorPositionX = 1920, monitorPositionY = 0, monitorWidth = 2560 },
                },
            });

            var monitors = await test.Model.GetMonitorsAsync().WithTimeoutAsync(Timeout);

            Assert.Equal(new[] { "DELL(0)", "LG(1)" }, monitors.Select(monitor => monitor.MonitorName).ToArray());
            Assert.Equal(2560, monitors[1].MonitorWidth);
        }

        [Fact]
        public async Task AProjectorOfAMix_IsOpenedOnAMonitorOrInAWindow()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("OpenVideoMixProjector");

            await test.Model.OpenVideoMixProjectorAsync(MixType.OBS_WEBSOCKET_VIDEO_MIX_TYPE_PROGRAM, 1).WithTimeoutAsync(Timeout);
            await test.Model.OpenVideoMixProjectorAsync(MixType.OBS_WEBSOCKET_VIDEO_MIX_TYPE_MULTIVIEW, geometry: "AdnQywADAAA=").WithTimeoutAsync(Timeout);

            var sent = test.AllSent("OpenVideoMixProjector");
            Assert.Equal("OBS_WEBSOCKET_VIDEO_MIX_TYPE_PROGRAM", sent[0].GetProperty("videoMixType").GetString());
            Assert.Equal(1, sent[0].GetProperty("monitorIndex").GetInt32());
            Assert.False(sent[0].TryGetProperty("projectorGeometry", out _));
            Assert.Equal("OBS_WEBSOCKET_VIDEO_MIX_TYPE_MULTIVIEW", sent[1].GetProperty("videoMixType").GetString());
            Assert.Equal("AdnQywADAAA=", sent[1].GetProperty("projectorGeometry").GetString());
            Assert.False(sent[1].TryGetProperty("monitorIndex", out _));
        }

        [Fact]
        public async Task AProjectorWithAMonitorAndAGeometry_IsRefusedByOBSStudio_AsAResponseException()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("OpenVideoMixProjector", 404, "`monitorIndex` and `projectorGeometry` are mutually exclusive.");

            var exception = await Assert.ThrowsAsync<ObsResponseException>(
                () => test.Model.OpenVideoMixProjectorAsync(MixType.OBS_WEBSOCKET_VIDEO_MIX_TYPE_PREVIEW, 0, "AdnQywADAAA=").WithTimeoutAsync(Timeout));

            Assert.Equal(RequestStatusCode.TooManyRequestFields, exception.ErrorCode);
        }

        [Fact]
        public async Task AProjectorOfASource_IsOpenedWithTheUuidOfTheSource()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("OpenSourceProjector");

            await test.Camera.OpenProjectorAsync(0).WithTimeoutAsync(Timeout);
            await test.Gaming.OpenProjectorAsync().WithTimeoutAsync(Timeout);

            var sent = test.AllSent("OpenSourceProjector");
            Assert.Equal(test.Camera.Uuid, sent[0].GetProperty("sourceUuid").GetGuid());
            Assert.Equal(0, sent[0].GetProperty("monitorIndex").GetInt32());
            Assert.Equal(test.Gaming.Uuid, sent[1].GetProperty("sourceUuid").GetGuid());
            Assert.False(sent[1].TryGetProperty("monitorIndex", out _));
        }

        [Fact]
        public async Task WhetherASourceIsOnTheAir_IsAskedForByItsUuid_ForScenesAndInputs()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("GetSourceActive", request => request!.Value.GetProperty("sourceUuid").GetGuid() == test.Camera.Uuid ? (100, new { videoActive = true, videoShowing = false }) : (100, new { videoActive = false, videoShowing = true }));

            var camera = await test.Camera.GetActivityAsync().WithTimeoutAsync(Timeout);
            var gaming = await test.Gaming.GetActivityAsync().WithTimeoutAsync(Timeout);

            Assert.True(camera.VideoActive);
            Assert.False(camera.VideoShowing);
            Assert.False(gaming.VideoActive);
            Assert.True(gaming.VideoShowing);
        }

        [Fact]
        public async Task AScreenshot_IsAskedForInTheFormatAndTheSizeThatWasGiven_AndReturnedAsTheImage()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("GetSourceScreenshot", 100, new { imageData = "data:image/png;base64,AQIDBA==" });

            var screenshot = await test.Gaming.GetScreenshotAsync("png", 640, 360, 80).WithTimeoutAsync(Timeout);
            var plain = await test.Camera.GetScreenshotAsync().WithTimeoutAsync(Timeout);

            Assert.Equal("png", screenshot.Format);
            Assert.Equal("image/png", screenshot.MediaType);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, screenshot.Data.ToArray());
            Assert.Equal("data:image/png;base64,AQIDBA==", screenshot.ToDataUri());
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, plain.Data.ToArray());
            var sent = test.AllSent("GetSourceScreenshot");
            Assert.Equal(test.Gaming.Uuid, sent[0].GetProperty("sourceUuid").GetGuid());
            Assert.Equal("png", sent[0].GetProperty("imageFormat").GetString());
            Assert.Equal(640, sent[0].GetProperty("imageWidth").GetInt32());
            Assert.Equal(360, sent[0].GetProperty("imageHeight").GetInt32());
            Assert.Equal(80, sent[0].GetProperty("imageCompressionQuality").GetInt32());
            Assert.False(sent[1].TryGetProperty("imageWidth", out _));
            Assert.False(sent[1].TryGetProperty("imageCompressionQuality", out _));
        }

        [Fact]
        public async Task AScreenshotThatIsSavedToAFile_CanBeWrittenByTheApplication()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("GetSourceScreenshot", 100, new { imageData = "data:image/jpg;base64,AQIDBA==" });
            var path = Path.Combine(Path.GetTempPath(), $"obsclient-test-{Guid.NewGuid():N}.jpg");
            try
            {
                var screenshot = await test.Gaming.GetScreenshotAsync("jpg").WithTimeoutAsync(Timeout);

                await screenshot.SaveAsync(path);

                Assert.Equal("image/jpg", screenshot.MediaType);
                Assert.Equal(new byte[] { 1, 2, 3, 4 }, await File.ReadAllBytesAsync(path));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Theory]
        [InlineData(7, null, null)]
        [InlineData(4097, null, null)]
        [InlineData(null, 0, null)]
        [InlineData(null, 5000, null)]
        [InlineData(null, null, -2)]
        [InlineData(null, null, 101)]
        public async Task AScreenshotWithNumbersThatOBSStudioRefuses_IsRefusedBeforeAnythingIsSent(int? width, int? height, int? quality)
        {
            await using var test = await CallFixture.StartAsync();

            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = test.Gaming.GetScreenshotAsync("png", width, height, quality); });
            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = test.Gaming.SaveScreenshotAsync("C:\\a.png", "png", width, height, quality); });
            Assert.Equal(0, test.Count("GetSourceScreenshot"));
            Assert.Equal(0, test.Count("SaveSourceScreenshot"));
        }

        [Fact]
        public async Task ABoundaryOfTheScreenshotNumbers_IsAllowed()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("GetSourceScreenshot", 100, new { imageData = "data:image/png;base64,AQIDBA==" });

            await test.Gaming.GetScreenshotAsync("png", 8, 4096, -1).WithTimeoutAsync(Timeout);
            await test.Gaming.GetScreenshotAsync("png", 4096, 8, 100).WithTimeoutAsync(Timeout);
            await test.Gaming.GetScreenshotAsync("png", quality: 0).WithTimeoutAsync(Timeout);

            Assert.Equal(3, test.Count("GetSourceScreenshot"));
        }

        [Fact]
        public async Task AScreenshotThatOBSStudioSavesToAFile_IsAskedForWithThePathAndTheFormat()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("SaveSourceScreenshot");

            await test.Camera.SaveScreenshotAsync("C:\\Pictures\\camera.jpg", "jpg", 1280, null, 90).WithTimeoutAsync(Timeout);

            var sent = Assert.Single(test.AllSent("SaveSourceScreenshot"));
            Assert.Equal(test.Camera.Uuid, sent.GetProperty("sourceUuid").GetGuid());
            Assert.Equal("C:\\Pictures\\camera.jpg", sent.GetProperty("imageFilePath").GetString());
            Assert.Equal("jpg", sent.GetProperty("imageFormat").GetString());
            Assert.Equal(1280, sent.GetProperty("imageWidth").GetInt32());
            Assert.Equal(90, sent.GetProperty("imageCompressionQuality").GetInt32());
        }

        [Fact]
        public async Task AScreenshotToAFolderThatDoesNotExist_IsAResponseException_AndTheSourceIsNotTakenForRemoved()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("SaveSourceScreenshot", 600, "The directory for your file path does not exist.");

            var exception = await Assert.ThrowsAsync<ObsResponseException>(() => test.Camera.SaveScreenshotAsync("Z:\\nowhere\\camera.png").WithTimeoutAsync(Timeout));

            Assert.Equal(RequestStatusCode.ResourceNotFound, exception.ErrorCode);
            Assert.Contains("directory", exception.Message);
            Assert.False(test.Camera.IsRemoved);
        }

        [Fact]
        public async Task AScreenshotOfASourceThatOBSStudioDoesNotKnow_IsARemovedException()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("SaveSourceScreenshot", 600, "No source was found by the UUID of `x`.");
            test.Answer("GetSourceScreenshot", 600, "No source was found by the UUID of `x`.");

            var saved = await Assert.ThrowsAsync<ObsRemovedException>(() => test.Camera.SaveScreenshotAsync("C:\\a.png").WithTimeoutAsync(Timeout));
            var taken = await Assert.ThrowsAsync<ObsRemovedException>(() => test.Camera.GetScreenshotAsync().WithTimeoutAsync(Timeout));

            Assert.Equal(ObsObjectState.Gone, saved.State);
            Assert.Equal(ObsObjectState.Gone, taken.State);
        }

        [Fact]
        public async Task AFormatThatOBSStudioCannotMake_IsAResponseException()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("GetSourceScreenshot", 400, "The field `imageFormat` must be one of the supported formats.");

            var exception = await Assert.ThrowsAsync<ObsResponseException>(() => test.Camera.GetScreenshotAsync("webp").WithTimeoutAsync(Timeout));

            Assert.Equal(RequestStatusCode.InvalidRequestField, exception.ErrorCode);
        }

        [Fact]
        public async Task TheCallsOfASource_AreRefused_WhenOBSStudioDestroyedIt_AndNothingIsSent()
        {
            await using var test = await CallFixture.StartAsync();
            var spare = test.Rig.Model.Inputs["Spare"];
            await test.Rig.Responder.SendEventAsync("InputRemoved", new { inputName = "Spare", inputUuid = spare.Uuid }, InputEvents);
            await ModelRig.EventuallyAsync(() => spare.State == ObsObjectState.Gone, "the input to be gone");

            await Assert.ThrowsAsync<ObsRemovedException>(() => spare.GetActivityAsync().WithTimeoutAsync(Timeout));
            await Assert.ThrowsAsync<ObsRemovedException>(() => spare.GetScreenshotAsync().WithTimeoutAsync(Timeout));
            await Assert.ThrowsAsync<ObsRemovedException>(() => spare.SaveScreenshotAsync("C:\\a.png").WithTimeoutAsync(Timeout));
            await Assert.ThrowsAsync<ObsRemovedException>(() => spare.OpenProjectorAsync().WithTimeoutAsync(Timeout));
            await Assert.ThrowsAsync<ObsRemovedException>(() => spare.TriggerHotkeyAsync("libobs.mute").WithTimeoutAsync(Timeout));
            await Assert.ThrowsAsync<ObsRemovedException>(() => spare.OpenPropertiesDialogAsync().WithTimeoutAsync(Timeout));
            await Assert.ThrowsAsync<ObsRemovedException>(() => spare.OpenFiltersDialogAsync().WithTimeoutAsync(Timeout));
            await Assert.ThrowsAsync<ObsRemovedException>(() => spare.OpenInteractDialogAsync().WithTimeoutAsync(Timeout));

            foreach (var type in new[] { "GetSourceActive", "GetSourceScreenshot", "SaveSourceScreenshot", "OpenSourceProjector", "TriggerHotkeyByName", "OpenInputPropertiesDialog", "OpenInputFiltersDialog", "OpenInputInteractDialog" })
            {
                Assert.Equal(0, test.Count(type));
            }
        }

        [Fact]
        public async Task ASourceThatOBSStudioRemovedAndKeepsAlive_CanStillBeAskedAbout()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("GetSourceActive", 100, new { videoActive = false, videoShowing = false });
            var camera = test.Camera;
            await test.Rig.Responder.SendEventAsync("InputRemoved", new { inputName = "Camera", inputUuid = camera.Uuid }, InputEvents);
            await ModelRig.EventuallyAsync(() => camera.IsRemoved, "the input to be removed");
            Assert.Equal(ObsObjectState.Detached, camera.State);

            var activity = await camera.GetActivityAsync().WithTimeoutAsync(Timeout);

            Assert.False(activity.VideoActive);
            Assert.Equal(1, test.Count("GetSourceActive"));
        }

        [Fact]
        public async Task TheDialogsOfAnInput_AreOpenedByItsUuid()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("OpenInputPropertiesDialog");
            test.Answer("OpenInputFiltersDialog");
            test.Answer("OpenInputInteractDialog");

            await test.Mic.OpenPropertiesDialogAsync().WithTimeoutAsync(Timeout);
            await test.Mic.OpenFiltersDialogAsync().WithTimeoutAsync(Timeout);
            await test.Mic.OpenInteractDialogAsync().WithTimeoutAsync(Timeout);

            foreach (var type in new[] { "OpenInputPropertiesDialog", "OpenInputFiltersDialog", "OpenInputInteractDialog" })
            {
                var sent = Assert.Single(test.AllSent(type));
                Assert.Equal(test.Mic.Uuid, sent.GetProperty("inputUuid").GetGuid());
            }
        }

        [Fact]
        public async Task TheInteractionWindowOfAnInputThatDoesNotSupportIt_IsAResponseException()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("OpenInputInteractDialog", 604, "The specified input does not support interaction.");

            var exception = await Assert.ThrowsAsync<ObsResponseException>(() => test.Mic.OpenInteractDialogAsync().WithTimeoutAsync(Timeout));

            Assert.Equal(RequestStatusCode.InvalidResourceState, exception.ErrorCode);
            Assert.Contains("does not support interaction", exception.Message);
        }

        [Fact]
        public async Task ARequestThatTheObsWebSocketOfOBSStudioDoesNotHave_IsAResponseException_ThatSaysSo()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("GetStats", 204, "Unknown request type.");

            var exception = await Assert.ThrowsAsync<ObsResponseException>(() => test.Model.GetStatsAsync().WithTimeoutAsync(Timeout));

            Assert.Equal(RequestStatusCode.UnknownRequestType, exception.ErrorCode);
            Assert.Contains("WebSocketVersion", exception.Message);
            Assert.Contains("statistics", exception.Message);
        }

        [Fact]
        public async Task AQuestion_IsAskedInItsTurn_AfterTheChangesThatWereAskedForBeforeIt()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("TriggerHotkeyByName", -1, null);
            test.Answer("GetStats", 100, new { cpuUsage = 1.0f });

            var change = test.Model.TriggerHotkeyAsync("ReplayBuffer.Save");
            await ModelRig.EventuallyAsync(() => test.Rig.Responder.HeldCount == 1, "the change to be held");
            var question = test.Model.GetStatsAsync();
            await Task.Delay(200);

            // The question waits behind the change, as the changes do among themselves.
            Assert.False(question.IsCompleted);
            Assert.Equal(0, test.Count("GetStats"));

            await test.Rig.Responder.AnswerHeldAsync();
            await Task.WhenAll(change, question).WithTimeoutAsync(Timeout);

            Assert.Equal(1, test.Count("GetStats"));
        }
        [Fact]
        public async Task AQuestionThatIsNotAnswered_EndsWithATimeoutException_AndTheLineGoesOn()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("GetStats", 0, null);

            var timeout = await Assert.ThrowsAsync<TimeoutException>(() => test.Model.GetStatsAsync().WithTimeoutAsync(Timeout));

            Assert.Contains("asking for the statistics of OBS Studio", timeout.Message);

            // Nothing was changed, so nothing is unknown, and the model was not read again for it.
            test.Answer("GetStats", 100, new { cpuUsage = 2.0f });
            var stats = await test.Model.GetStatsAsync().WithTimeoutAsync(Timeout);
            Assert.Equal(2.0f, stats.CpuUsage);
        }

        [Fact]
        public async Task AQuestionWhoseConnectionEnds_IsANotConnectedException()
        {
            await using var test = await CallFixture.StartAsync(new ObsModelOptions { AutoReconnect = false, WriteTimeout = TimeSpan.FromSeconds(5) });
            test.Rig.Responder.OnRequest = (type, data) =>
            {
                test.Record(type, data);
                if (type == "GetStats")
                {
                    test.Rig.Responder.AbortConnection();
                    return (0, null);
                }

                return null;
            };

            await Assert.ThrowsAsync<ObsNotConnectedException>(() => test.Model.GetStatsAsync().WithTimeoutAsync(Timeout));
        }

        [Fact]
        public async Task AQuestion_IsNotAsked_WithoutAConnection()
        {
            await using var rig = ModelRig.Create();

            await Assert.ThrowsAsync<ObsNotConnectedException>(() => rig.Model.GetStatsAsync().WithTimeoutAsync(Timeout));
            await Assert.ThrowsAsync<ObsNotConnectedException>(() => rig.Model.GetHotkeysAsync().WithTimeoutAsync(Timeout));
            await Assert.ThrowsAsync<ObsNotConnectedException>(() => rig.Model.GetMonitorsAsync().WithTimeoutAsync(Timeout));
            await Assert.ThrowsAsync<ObsNotConnectedException>(() => rig.Model.Outputs.GetAllAsync().WithTimeoutAsync(Timeout));
            await Assert.ThrowsAsync<ObsNotConnectedException>(() => rig.Model.TriggerHotkeyAsync("a").WithTimeoutAsync(Timeout));
            await Assert.ThrowsAsync<ObsNotConnectedException>(() => rig.Model.BroadcastCustomEventAsync(new { a = 1 }).WithTimeoutAsync(Timeout));

            Assert.Empty(rig.Responder.RequestLog);
        }

        [Fact]
        public async Task AQuestion_IsNotAsked_WhileOBSStudioChangesItsSceneCollection()
        {
            await using var test = await CallFixture.StartAsync();
            await test.Rig.Responder.SendEventAsync("CurrentSceneCollectionChanging", new { sceneCollectionName = "Untitled" }, 2);
            await ModelRig.EventuallyAsync(() => test.Rig.Model.Host.Gate.Switching, "the host to know that OBS Studio is switching");

            await Assert.ThrowsAsync<ObsNotReadyException>(() => test.Model.GetStatsAsync().WithTimeoutAsync(Timeout));
            await Assert.ThrowsAsync<ObsNotReadyException>(() => test.Camera.GetScreenshotAsync().WithTimeoutAsync(Timeout));

            Assert.Equal(0, test.Count("GetStats"));
            Assert.Equal(0, test.Count("GetSourceScreenshot"));
        }

        [Fact]
        public async Task AQuestionThatWaitsForItsTurn_IsNotAsked_WhenTheCallerGivesUp()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("TriggerHotkeyByName", -1, null);
            test.Answer("GetStats", 100, new { cpuUsage = 1.0f });
            var change = test.Model.TriggerHotkeyAsync("ReplayBuffer.Save");
            await ModelRig.EventuallyAsync(() => test.Rig.Responder.HeldCount == 1, "the change to be held");
            using var cancellation = new CancellationTokenSource();
            var question = test.Model.GetStatsAsync(cancellation.Token);
            await Task.Delay(100);

            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => question.WithTimeoutAsync(Timeout));
            await test.Rig.Responder.AnswerHeldAsync();
            await change.WithTimeoutAsync(Timeout);
            await Task.Delay(200);
            Assert.Equal(0, test.Count("GetStats"));
        }
        [Fact]
        public async Task AnAnswerThatCannotBeRead_IsAClientException_AndNotAnUnknownOutcome()
        {
            await using var test = await CallFixture.StartAsync();
            test.Rig.Responder.OnRequest = (type, data) =>
            {
                test.Record(type, data);
                return type == "GetStats" ? (-2, null) : null;
            };

            var exception = await Assert.ThrowsAsync<ObsClientException>(() => test.Model.GetStatsAsync().WithTimeoutAsync(Timeout));

            Assert.IsNotType<ObsUnknownOutcomeException>(exception);
            Assert.Contains("can not be read", exception.Message);
        }

        [Fact]
        public async Task ADisposedModel_EndsAQuestionThatWaitsForItsAnswer_WithAnObjectDisposedException()
        {
            var test = await CallFixture.StartAsync(new ObsModelOptions { WriteTimeout = TimeSpan.FromSeconds(30) });
            test.Answer("GetStats", 0, null);
            var question = test.Model.GetStatsAsync();
            await ModelRig.EventuallyAsync(() => test.Count("GetStats") == 1, "the question to be sent");

            await test.DisposeAsync();

            await Assert.ThrowsAsync<ObjectDisposedException>(() => question.WithTimeoutAsync(Timeout));
        }
    }
}
