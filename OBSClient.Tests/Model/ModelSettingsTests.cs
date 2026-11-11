namespace OBSStudioClient.Tests.Model
{
    using System.Text.Json;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Model;
    using OBSStudioClient.Tests.TestSupport;

    /// <summary>
    /// The settings of the inputs and the filters, the balance, sync offset, monitor type and tracks of the audio of an input, and the calls around them (the kinds and their defaults, the choices
    /// of a list property, a button of the properties, the global audio inputs, the deinterlacing): what the model shows, what it sends, and how the calls end.
    /// </summary>
    public class ModelSettingsTests
    {
        private const int InputEvents = 8;
        private const int FilterEvents = 32;
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private static Guid UuidOf(FakeWorld world, string name)
        {
            return world.Inputs.First(input => input.Name == name).Uuid;
        }

        private static int Told<T>(List<T> list)
        {
            lock (list)
            {
                return list.Count;
            }
        }

        private static List<string> Watch(ObsInput input)
        {
            var raised = new List<string>();
            input.PropertyChanged += (_, e) =>
            {
                lock (raised)
                {
                    raised.Add(e.PropertyName!);
                }
            };
            return raised;
        }

        // OBS Studio makes the change and says so: the world holds it, and the event follows the answer.
        private static void AnswerWithEvent(CallFixture test, string request, Action<FakeWorld> change, string eventType, object eventData, int intent = InputEvents)
        {
            test.Answer(request, data =>
            {
                test.Rig.Responder.Change(change);
                _ = Task.Run(() => test.Rig.Responder.SendEventAsync(eventType, eventData, intent));
                return (100, null);
            });
        }

        [Fact]
        public async Task TheSettingsAndTheAudioDetails_AreKnownWhenTheInputsAreAnnounced()
        {
            await using var test = await CallFixture.StartAsync(setup: world =>
            {
                var camera = UuidOf(world, "Camera");
                var mic = UuidOf(world, "Mic");
                world.InputSettings[camera] = new { width = 640, font = new { size = 48 } };
                world.AudioBalances[mic] = 0.25f;
                world.SyncOffsets[mic] = 150;
                world.MonitorTypes[mic] = "OBS_MONITORING_TYPE_MONITOR_ONLY";
                world.Tracks[mic] = 0b101101;
            });

            Assert.Equal(640, test.Camera.Settings.GetProperty("width").GetInt32());
            Assert.Equal(48, test.Camera.Settings.GetProperty("font").GetProperty("size").GetInt32());
            Assert.Equal(JsonValueKind.Object, test.Mic.Settings.ValueKind);
            Assert.Empty(test.Mic.Settings.EnumerateObject());
            Assert.Equal(0.25f, test.Mic.AudioBalance);
            Assert.Equal(TimeSpan.FromMilliseconds(150), test.Mic.SyncOffset);
            Assert.Equal(ObsMonitorType.MonitorOnly, test.Mic.MonitorType);
            Assert.Equal(ObsAudioTracks.Track1 | ObsAudioTracks.Track3 | ObsAudioTracks.Track4 | ObsAudioTracks.Track6, test.Mic.AudioTracks);
        }

        [Fact]
        public async Task AnInputThatHasNoSettingsSet_HasAnEmptyObject_AndTheDefaultsOfItsAudio()
        {
            await using var test = await CallFixture.StartAsync();

            Assert.Equal(JsonValueKind.Object, test.Camera.Settings.ValueKind);
            Assert.Equal("{}", test.Camera.Settings.GetRawText());
            Assert.Equal((0.5f, TimeSpan.Zero, ObsMonitorType.Off, ObsAudioTracks.All), (test.Mic.AudioBalance, test.Mic.SyncOffset, test.Mic.MonitorType, test.Mic.AudioTracks));
        }

        [Fact]
        public async Task TheEventsOfTheSettingsAndTheAudio_ChangeTheProperties_AndAreToldAsPropertyChangesAndRecords()
        {
            await using var test = await CallFixture.StartAsync();
            var raised = Watch(test.Mic);
            var uuid = test.Mic.Uuid;

            await test.Rig.Responder.SendEventAsync("InputSettingsChanged", new { inputName = "Mic", inputUuid = uuid, inputSettings = new { device_id = "usb" } }, InputEvents);
            await test.Rig.Responder.SendEventAsync("InputAudioBalanceChanged", new { inputName = "Mic", inputUuid = uuid, inputAudioBalance = 0.75 }, InputEvents);
            await test.Rig.Responder.SendEventAsync("InputAudioSyncOffsetChanged", new { inputName = "Mic", inputUuid = uuid, inputAudioSyncOffset = -100 }, InputEvents);
            await test.Rig.Responder.SendEventAsync("InputAudioMonitorTypeChanged", new { inputName = "Mic", inputUuid = uuid, monitorType = "OBS_MONITORING_TYPE_MONITOR_AND_OUTPUT" }, InputEvents);
            await test.Rig.Responder.SendEventAsync("InputAudioTracksChanged", new { inputName = "Mic", inputUuid = uuid, inputAudioTracks = new Dictionary<string, bool> { ["1"] = true, ["2"] = true, ["3"] = false, ["4"] = false, ["5"] = false, ["6"] = false } }, InputEvents);
            await ModelRig.EventuallyAsync(() => Told(raised) == 5, "the five changes to be told");

            Assert.Equal(new[] { "Settings", "AudioBalance", "SyncOffset", "MonitorType", "AudioTracks" }, raised.ToArray());
            Assert.Equal("usb", test.Mic.Settings.GetProperty("device_id").GetString());
            Assert.Equal((0.75f, TimeSpan.FromMilliseconds(-100), ObsMonitorType.MonitorAndOutput, ObsAudioTracks.Track1 | ObsAudioTracks.Track2), (test.Mic.AudioBalance, test.Mic.SyncOffset, test.Mic.MonitorType, test.Mic.AudioTracks));
            var records = test.Rig.Changes.Where(change => ReferenceEquals(change.Entity, test.Mic) && change.PropertyName is "Settings" or "AudioBalance" or "SyncOffset" or "MonitorType" or "AudioTracks").ToArray();
            Assert.Equal(new[] { "Settings", "AudioBalance", "SyncOffset", "MonitorType", "AudioTracks" }, records.Select(change => change.PropertyName!).ToArray());
            Assert.Equal("{}", ((JsonElement)records[0].OldValue!).GetRawText());
            Assert.Equal("""{"device_id":"usb"}""", ((JsonElement)records[0].NewValue!).GetRawText());
            Assert.Equal((TimeSpan.Zero, TimeSpan.FromMilliseconds(-100)), ((TimeSpan)records[2].OldValue!, (TimeSpan)records[2].NewValue!));
            Assert.Equal(ObsAudioTracks.All, (ObsAudioTracks)records[4].OldValue!);
            Assert.Contains("Settings", records[0].ToString());
        }

        [Fact]
        public async Task TheSameSettingsAgain_AreNoChange()
        {
            await using var test = await CallFixture.StartAsync(setup: world => world.InputSettings[UuidOf(world, "Camera")] = new { width = 640, height = 360 });
            var raised = Watch(test.Camera);

            await test.Rig.Responder.SendEventAsync("InputSettingsChanged", new { inputName = "Camera", inputUuid = test.Camera.Uuid, inputSettings = new { height = 360, width = 640 } }, InputEvents);
            await test.Rig.Responder.SendEventAsync("InputSettingsChanged", new { inputName = "Camera", inputUuid = test.Camera.Uuid, inputSettings = new { height = 360, width = 641 } }, InputEvents);
            await ModelRig.EventuallyAsync(() => Told(raised) == 1, "the change of the width to be told");

            Assert.Equal(new[] { "Settings" }, raised.ToArray());
            Assert.Equal(641, test.Camera.Settings.GetProperty("width").GetInt32());
        }

        [Fact]
        public async Task TheSettings_AreChanged_AndTheCallReturnsWhenAnEventShowsThem_WithoutReadingThemAgain()
        {
            await using var test = await CallFixture.StartAsync();
            var camera = test.Camera;
            AnswerWithEvent(test, "SetInputSettings", world => world.InputSettings[camera.Uuid] = new { width = 800 }, "InputSettingsChanged", new { inputName = "Camera", inputUuid = camera.Uuid, inputSettings = new { width = 800 } });
            var before = test.Count("GetInputSettings");

            await camera.SetSettingsAsync(new { width = 800 }).WithTimeoutAsync(Timeout);

            Assert.Equal(800, camera.Settings.GetProperty("width").GetInt32());
            var sent = Assert.Single(test.AllSent("SetInputSettings"));
            Assert.Equal(camera.Uuid, sent.GetProperty("inputUuid").GetGuid());
            Assert.Equal(800, sent.GetProperty("inputSettings").GetProperty("width").GetInt32());
            Assert.True(sent.GetProperty("overlay").GetBoolean());
            Assert.Equal(before, test.Count("GetInputSettings"));
        }

        [Fact]
        public async Task TheSettings_AreShownByARead_WhenNoEventComes()
        {
            await using var test = await CallFixture.StartAsync();
            var camera = test.Camera;
            test.Answer("SetInputSettings", _ =>
            {
                test.Rig.Responder.Change(world => world.InputSettings[camera.Uuid] = new { width = 800 });
                return (100, null);
            });
            var before = test.Count("GetInputSettings");

            await camera.SetSettingsAsync(new Dictionary<string, object?> { ["width"] = 800 }).WithTimeoutAsync(Timeout);

            Assert.Equal(800, camera.Settings.GetProperty("width").GetInt32());
            Assert.True(test.Count("GetInputSettings") > before);
        }

        [Fact]
        public async Task SettingsThatOBSStudioChangesOnTheWayIn_AreNotAnError_TheModelShowsWhatItHolds()
        {
            await using var test = await CallFixture.StartAsync();
            var camera = test.Camera;

            // OBS Studio keeps 801 for the 800 that was asked: the events and the read show 801, and the call has done what it could.
            AnswerWithEvent(test, "SetInputSettings", world => world.InputSettings[camera.Uuid] = new { width = 801 }, "InputSettingsChanged", new { inputName = "Camera", inputUuid = camera.Uuid, inputSettings = new { width = 801 } });

            await camera.SetSettingsAsync(new { width = 800 }).WithTimeoutAsync(Timeout);

            Assert.Equal(801, camera.Settings.GetProperty("width").GetInt32());
        }

        [Fact]
        public async Task SettingsThatAreChangedWithoutAnOverlay_AreSentSo_AndAreShownWhenTheyAreAllThatIsLeft()
        {
            await using var test = await CallFixture.StartAsync(setup: world => world.InputSettings[UuidOf(world, "Camera")] = new { a = 0, b = 2 });
            var camera = test.Camera;
            AnswerWithEvent(test, "SetInputSettings", world => world.InputSettings[camera.Uuid] = new { a = 1 }, "InputSettingsChanged", new { inputName = "Camera", inputUuid = camera.Uuid, inputSettings = new { a = 1 } });
            var before = test.Count("GetInputSettings");

            await camera.SetSettingsAsync(new { a = 1 }, overlay: false).WithTimeoutAsync(Timeout);

            Assert.False(test.AllSent("SetInputSettings").Single().GetProperty("overlay").GetBoolean());
            Assert.Equal("""{"a":1}""", camera.Settings.GetRawText());
            Assert.Equal(before, test.Count("GetInputSettings"));
        }

        [Fact]
        public async Task SettingsThatAreNotAnObject_OrAreACancellationToken_AreAnArgumentException_AndNothingIsSent()
        {
            await using var test = await CallFixture.StartAsync();
            using var source = new CancellationTokenSource();

            var number = Assert.Throws<ArgumentException>(() => { _ = test.Camera.SetSettingsAsync(5); });
            var token = Assert.Throws<ArgumentException>(() => { _ = test.Camera.SetSettingsAsync(source.Token); });

            Assert.Contains("must be an object", number.Message);
            Assert.Equal("settings", token.ParamName);
            Assert.Contains("Pass it by name (cancellationToken: token)", token.Message);
            Assert.Equal(0, test.Count("SetInputSettings"));
        }

        [Fact]
        public async Task TheAudioDetails_AreChanged_WithTheValuesThatWereGiven_AndTheCallsReturnWhenTheEventsShowThem()
        {
            await using var test = await CallFixture.StartAsync();
            var mic = test.Mic;
            AnswerWithEvent(test, "SetInputAudioBalance", world => world.AudioBalances[mic.Uuid] = 0.25f, "InputAudioBalanceChanged", new { inputName = "Mic", inputUuid = mic.Uuid, inputAudioBalance = 0.25 });
            AnswerWithEvent(test, "SetInputAudioSyncOffset", world => world.SyncOffsets[mic.Uuid] = 150, "InputAudioSyncOffsetChanged", new { inputName = "Mic", inputUuid = mic.Uuid, inputAudioSyncOffset = 150 });
            AnswerWithEvent(test, "SetInputAudioMonitorType", world => world.MonitorTypes[mic.Uuid] = "OBS_MONITORING_TYPE_MONITOR_ONLY", "InputAudioMonitorTypeChanged", new { inputName = "Mic", inputUuid = mic.Uuid, monitorType = "OBS_MONITORING_TYPE_MONITOR_ONLY" });
            AnswerWithEvent(
                test,
                "SetInputAudioTracks",
                world => world.Tracks[mic.Uuid] = 0b000101,
                "InputAudioTracksChanged",
                new { inputName = "Mic", inputUuid = mic.Uuid, inputAudioTracks = new Dictionary<string, bool> { ["1"] = true, ["2"] = false, ["3"] = true, ["4"] = false, ["5"] = false, ["6"] = false } });
            var before = test.Count("GetInputMute");

            await mic.SetAudioBalanceAsync(0.25f).WithTimeoutAsync(Timeout);
            await mic.SetSyncOffsetAsync(TimeSpan.FromMilliseconds(150)).WithTimeoutAsync(Timeout);
            await mic.SetMonitorTypeAsync(ObsMonitorType.MonitorOnly).WithTimeoutAsync(Timeout);
            await mic.SetAudioTracksAsync(ObsAudioTracks.Track1 | ObsAudioTracks.Track3).WithTimeoutAsync(Timeout);

            Assert.Equal((0.25f, TimeSpan.FromMilliseconds(150), ObsMonitorType.MonitorOnly, ObsAudioTracks.Track1 | ObsAudioTracks.Track3), (mic.AudioBalance, mic.SyncOffset, mic.MonitorType, mic.AudioTracks));
            Assert.Equal(0.25, test.AllSent("SetInputAudioBalance").Single().GetProperty("inputAudioBalance").GetDouble());
            Assert.Equal(150, test.AllSent("SetInputAudioSyncOffset").Single().GetProperty("inputAudioSyncOffset").GetInt32());
            Assert.Equal("OBS_MONITORING_TYPE_MONITOR_ONLY", test.AllSent("SetInputAudioMonitorType").Single().GetProperty("monitorType").GetString());
            var tracks = test.AllSent("SetInputAudioTracks").Single().GetProperty("inputAudioTracks");
            Assert.Equal(new[] { true, false, true, false, false, false }, new[] { "1", "2", "3", "4", "5", "6" }.Select(track => tracks.GetProperty(track).GetBoolean()).ToArray());
            Assert.Equal(before, test.Count("GetInputMute"));
        }

        [Fact]
        public async Task TheAudioDetails_OfAnInputWithoutAudio_CanNotBeChanged_AndNothingIsSent()
        {
            await using var test = await CallFixture.StartAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() => test.Camera.SetAudioBalanceAsync(0.5f).WithTimeoutAsync(Timeout));
            await Assert.ThrowsAsync<InvalidOperationException>(() => test.Camera.SetSyncOffsetAsync(TimeSpan.Zero).WithTimeoutAsync(Timeout));
            await Assert.ThrowsAsync<InvalidOperationException>(() => test.Camera.SetMonitorTypeAsync(ObsMonitorType.Off).WithTimeoutAsync(Timeout));
            await Assert.ThrowsAsync<InvalidOperationException>(() => test.Camera.SetAudioTracksAsync(ObsAudioTracks.None).WithTimeoutAsync(Timeout));

            Assert.Equal(0, test.Count("SetInputAudioBalance") + test.Count("SetInputAudioSyncOffset") + test.Count("SetInputAudioMonitorType") + test.Count("SetInputAudioTracks"));
        }

        [Fact]
        public async Task TheAudioDetails_ThatAreOutOfRange_AreAnArgumentOutOfRangeException_AndNothingIsSent()
        {
            await using var test = await CallFixture.StartAsync();

            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = test.Mic.SetAudioBalanceAsync(-0.1f); });
            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = test.Mic.SetAudioBalanceAsync(1.1f); });
            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = test.Mic.SetAudioBalanceAsync(float.NaN); });
            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = test.Mic.SetSyncOffsetAsync(TimeSpan.FromMilliseconds(-951)); });
            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = test.Mic.SetSyncOffsetAsync(TimeSpan.FromMilliseconds(20001)); });
            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = test.Mic.SetMonitorTypeAsync((ObsMonitorType)7); });
            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = test.Mic.SetAudioTracksAsync((ObsAudioTracks)64); });

            Assert.Equal(0, test.Count("SetInputAudioBalance") + test.Count("SetInputAudioSyncOffset") + test.Count("SetInputAudioMonitorType") + test.Count("SetInputAudioTracks"));
        }

        [Fact]
        public async Task TheRangesOfTheAudioDetails_AreAllowedAtTheirEnds()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("SetInputAudioBalance", 100, null);
            test.Answer("SetInputAudioSyncOffset", 100, null);

            // The model shows the values after the read that follows the events that did not come: the calls end with it.
            await test.Mic.SetAudioBalanceAsync(0f).WithTimeoutAsync(Timeout).ConfigureAwait(true);
            await test.Mic.SetSyncOffsetAsync(TimeSpan.FromMilliseconds(20000)).WithTimeoutAsync(Timeout).ConfigureAwait(true);

            Assert.Equal(2, test.Count("SetInputAudioBalance") + test.Count("SetInputAudioSyncOffset"));
        }

        [Fact]
        public async Task ASyncOffsetWithAPartOfAMillisecond_IsRounded()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("SetInputAudioSyncOffset", 100, null);

            await test.Mic.SetSyncOffsetAsync(TimeSpan.FromTicks(1_500_000)).WithTimeoutAsync(Timeout);

            Assert.Equal(150, test.AllSent("SetInputAudioSyncOffset").Single().GetProperty("inputAudioSyncOffset").GetInt32());
        }

        [Fact]
        public async Task AMonitorTypeThatTheComputerCanNotDo_IsAResponseException_WithTheCodeOfOBSStudio()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("SetInputAudioMonitorType", 604, "Audio monitoring is not available on this platform.");

            var exception = await Assert.ThrowsAsync<ObsResponseException>(() => test.Mic.SetMonitorTypeAsync(ObsMonitorType.MonitorOnly).WithTimeoutAsync(Timeout));

            Assert.Equal(RequestStatusCode.InvalidResourceState, exception.ErrorCode);
            Assert.Contains("not available on this platform", exception.Message);
        }

        [Fact]
        public async Task TheDeinterlacing_IsAskedFor_AndSet_AsTheModelNamesIt()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("GetInputDeinterlaceMode", 100, new { inputDeinterlaceMode = "OBS_DEINTERLACE_MODE_YADIF_2X" });
            test.Answer("GetInputDeinterlaceFieldOrder", 100, new { inputDeinterlaceFieldOrder = "OBS_DEINTERLACE_FIELD_ORDER_BOTTOM" });
            test.Answer("SetInputDeinterlaceMode");
            test.Answer("SetInputDeinterlaceFieldOrder");

            var mode = await test.Camera.GetDeinterlaceModeAsync().WithTimeoutAsync(Timeout);
            var order = await test.Camera.GetDeinterlaceFieldOrderAsync().WithTimeoutAsync(Timeout);
            await test.Camera.SetDeinterlaceModeAsync(ObsDeinterlaceMode.Blend2x).WithTimeoutAsync(Timeout);
            await test.Camera.SetDeinterlaceFieldOrderAsync(ObsDeinterlaceFieldOrder.TopFieldFirst).WithTimeoutAsync(Timeout);

            Assert.Equal((ObsDeinterlaceMode.Yadif2x, ObsDeinterlaceFieldOrder.BottomFieldFirst), (mode, order));
            Assert.Equal("OBS_DEINTERLACE_MODE_BLEND_2X", test.AllSent("SetInputDeinterlaceMode").Single().GetProperty("inputDeinterlaceMode").GetString());
            Assert.Equal("OBS_DEINTERLACE_FIELD_ORDER_TOP", test.AllSent("SetInputDeinterlaceFieldOrder").Single().GetProperty("inputDeinterlaceFieldOrder").GetString());
        }

        [Fact]
        public async Task EveryDeinterlaceMode_HasAName_ThatOBSStudioKnows()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("SetInputDeinterlaceMode");

            foreach (var mode in Enum.GetValues<ObsDeinterlaceMode>())
            {
                await test.Camera.SetDeinterlaceModeAsync(mode).WithTimeoutAsync(Timeout);
            }

            var names = test.AllSent("SetInputDeinterlaceMode").Select(sent => sent.GetProperty("inputDeinterlaceMode").GetString()).ToArray();
            Assert.Equal(
                new[]
                {
                    "OBS_DEINTERLACE_MODE_DISABLE", "OBS_DEINTERLACE_MODE_DISCARD", "OBS_DEINTERLACE_MODE_RETRO", "OBS_DEINTERLACE_MODE_BLEND", "OBS_DEINTERLACE_MODE_BLEND_2X",
                    "OBS_DEINTERLACE_MODE_LINEAR", "OBS_DEINTERLACE_MODE_LINEAR_2X", "OBS_DEINTERLACE_MODE_YADIF", "OBS_DEINTERLACE_MODE_YADIF_2X",
                },
                names);
        }

        [Fact]
        public async Task TheDeinterlacingOfAnInputThatIsNotAsync_IsAResponseException_WithTheCodeOfOBSStudio()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("GetInputDeinterlaceMode", 604, "The specified input is not async.");

            var exception = await Assert.ThrowsAsync<ObsResponseException>(() => test.Mic.GetDeinterlaceModeAsync().WithTimeoutAsync(Timeout));

            Assert.Equal(RequestStatusCode.InvalidResourceState, exception.ErrorCode);
        }

        [Fact]
        public async Task TheDeinterlacing_OfAnObsWebsocketThatDoesNotHaveIt_SaysSo()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("GetInputDeinterlaceMode", 204, null);

            var exception = await Assert.ThrowsAsync<ObsResponseException>(() => test.Camera.GetDeinterlaceModeAsync().WithTimeoutAsync(Timeout));

            Assert.Equal(RequestStatusCode.UnknownRequestType, exception.ErrorCode);
            Assert.Contains("WebSocketVersion", exception.Message);
        }

        [Fact]
        public async Task TheChoicesOfAListProperty_AreAskedFor_AndAreTheModelsOwn()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("GetInputPropertiesListPropertyItems", 100, new
            {
                propertyItems = new object[]
                {
                    new { itemEnabled = true, itemName = "Display 1", itemValue = "id-1" },
                    new { itemEnabled = false, itemName = "Display 2 (gone)", itemValue = 2 },
                    new { itemEnabled = true, itemName = "Other", itemValue = (object?)null },
                },
            });

            var items = await test.Camera.GetPropertyItemsAsync("monitor_id").WithTimeoutAsync(Timeout);

            var sent = Assert.Single(test.AllSent("GetInputPropertiesListPropertyItems"));
            Assert.Equal((test.Camera.Uuid, "monitor_id"), (sent.GetProperty("inputUuid").GetGuid(), sent.GetProperty("propertyName").GetString()));
            Assert.Equal(new[] { "Display 1", "Display 2 (gone)", "Other" }, items.Select(item => item.Name).ToArray());
            Assert.Equal(new[] { true, false, true }, items.Select(item => item.IsEnabled).ToArray());
            Assert.Equal("id-1", items[0].Value!.Value.GetString());
            Assert.Equal(2, items[1].Value!.Value.GetInt32());
            Assert.True(items[2].Value is null || items[2].Value!.Value.ValueKind == JsonValueKind.Null);
            Assert.Equal("Display 2 (gone) (not enabled)", items[1].ToString());
        }

        [Fact]
        public async Task APropertyThatTheInputDoesNotHave_IsAResponseException_AndNotARemoval()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("GetInputPropertiesListPropertyItems", 600, "Unable to find a property by that name.");
            test.Answer("PressInputPropertiesButton", 600, "Unable to find a property by that name.");

            var items = await Assert.ThrowsAsync<ObsResponseException>(() => test.Camera.GetPropertyItemsAsync("nope").WithTimeoutAsync(Timeout));
            var press = await Assert.ThrowsAsync<ObsResponseException>(() => test.Camera.PressPropertyButtonAsync("nope").WithTimeoutAsync(Timeout));

            Assert.Equal(RequestStatusCode.ResourceNotFound, items.ErrorCode);
            Assert.Equal(RequestStatusCode.ResourceNotFound, press.ErrorCode);
        }

        [Fact]
        public async Task AnInputThatIsNotThere_IsARemoval_WhateverTheCallIs()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("GetInputPropertiesListPropertyItems", 600, "No input was found by the UUID `x`.");

            await Assert.ThrowsAsync<ObsRemovedException>(() => test.Camera.GetPropertyItemsAsync("monitor_id").WithTimeoutAsync(Timeout));
        }

        [Fact]
        public async Task AButtonOfTheProperties_IsPressedByItsName()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("PressInputPropertiesButton");

            await test.Camera.PressPropertyButtonAsync("refreshnocache").WithTimeoutAsync(Timeout);

            var sent = Assert.Single(test.AllSent("PressInputPropertiesButton"));
            Assert.Equal((test.Camera.Uuid, "refreshnocache"), (sent.GetProperty("inputUuid").GetGuid(), sent.GetProperty("propertyName").GetString()));
        }

        [Fact]
        public async Task ANameThatIsEmpty_IsAnArgumentException_ForThePropertyCalls()
        {
            await using var test = await CallFixture.StartAsync();

            Assert.Throws<ArgumentException>(() => { _ = test.Camera.GetPropertyItemsAsync(string.Empty); });
            Assert.Throws<ArgumentException>(() => { _ = test.Camera.PressPropertyButtonAsync(string.Empty); });
            Assert.Throws<ArgumentException>(() => { _ = test.Model.GetDefaultInputSettingsAsync(string.Empty); });
            Assert.Throws<ArgumentException>(() => { _ = test.Model.GetDefaultFilterSettingsAsync(string.Empty); });
        }

        [Fact]
        public async Task TheKindsOfInputsAndFilters_AreAskedFor()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("GetInputKindList", 100, new { inputKinds = new[] { "color_source_v3", "browser_source" } });
            test.Answer("GetSourceFilterKindList", 100, new { sourceFilterKinds = new[] { "color_filter_v2", "gain_filter" } });

            var versioned = await test.Model.GetInputKindsAsync().WithTimeoutAsync(Timeout);
            var unversioned = await test.Model.GetInputKindsAsync(unversioned: true).WithTimeoutAsync(Timeout);
            var filters = await test.Model.GetFilterKindsAsync().WithTimeoutAsync(Timeout);

            Assert.Equal(new[] { "color_source_v3", "browser_source" }, versioned);
            Assert.Equal(new[] { "color_source_v3", "browser_source" }, unversioned);
            Assert.Equal(new[] { "color_filter_v2", "gain_filter" }, filters);
            var sent = test.AllSent("GetInputKindList");
            Assert.Equal(new[] { false, true }, sent.Select(data => data.GetProperty("unversioned").GetBoolean()).ToArray());
        }

        [Fact]
        public async Task TheDefaultSettingsOfAKind_AreAskedFor_AsAJsonObject_AndAKindThatDoesNotExist_IsAResponseException()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("GetInputDefaultSettings", data => data!.Value.GetProperty("inputKind").GetString() == "nope" ? (605, "") : (100, new { defaultInputSettings = new { width = 1920, height = 1080, color = 4278190080L } }));
            test.Answer("GetSourceFilterDefaultSettings", data => data!.Value.GetProperty("filterKind").GetString() == "nope" ? (607, "") : (100, new { defaultFilterSettings = new { gamma = 0.0 } }));

            var input = await test.Model.GetDefaultInputSettingsAsync("color_source_v3").WithTimeoutAsync(Timeout);
            var filter = await test.Model.GetDefaultFilterSettingsAsync("color_filter_v2").WithTimeoutAsync(Timeout);
            var missingInput = await Assert.ThrowsAsync<ObsResponseException>(() => test.Model.GetDefaultInputSettingsAsync("nope").WithTimeoutAsync(Timeout));
            var missingFilter = await Assert.ThrowsAsync<ObsResponseException>(() => test.Model.GetDefaultFilterSettingsAsync("nope").WithTimeoutAsync(Timeout));

            Assert.Equal(1920, input.GetProperty("width").GetInt32());
            Assert.Equal(4278190080L, input.GetProperty("color").GetInt64());
            Assert.Equal(0.0, filter.GetProperty("gamma").GetDouble());
            Assert.Equal(RequestStatusCode.InvalidInputKind, missingInput.ErrorCode);
            Assert.Equal(RequestStatusCode.InvalidFilterKind, missingFilter.ErrorCode);
        }

        [Fact]
        public async Task TheGlobalAudioInputs_AreTheInputsOfTheModel_AndAreNullWhenNoneIsChosenOrTheModelDoesNotHaveIt()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("GetSpecialInputs", 100, new { desktop1 = (string?)null, desktop2 = "Mic", mic1 = "Camera", mic2 = "Not in the model", mic3 = (string?)null, mic4 = (string?)null });

            var special = await test.Model.GetSpecialInputsAsync().WithTimeoutAsync(Timeout);

            Assert.Null(special.Desktop1);
            Assert.Same(test.Mic, special.Desktop2);
            Assert.Same(test.Camera, special.Mic1);
            Assert.Null(special.Mic2);
            Assert.Null(special.Mic3);
            Assert.Null(special.Mic4);
            Assert.Contains("'Mic'", special.ToString());
        }

        [Fact]
        public async Task TheSettingsOfAFilter_AreAskedFor_AsAJsonObject()
        {
            await using var test = await CallFixture.StartAsync(setup: world =>
            {
                var mic = UuidOf(world, "Mic");
                world.AddFilter(mic, "Gain", "gain_filter");
                world.AddFilter(mic, "Gate", "noise_gate_filter");
                world.FilterSettings[(mic, "Gain")] = new { db = 3.5 };
            });
            var gain = test.Mic.Filters["Gain"];
            var gate = test.Mic.Filters["Gate"];
            test.Answer("GetSourceFilter", data => data!.Value.GetProperty("filterName").GetString() == "Gain"
                ? (100, new { filterEnabled = true, filterIndex = 0, filterKind = "gain_filter", filterSettings = new { db = 3.5 } })
                : (100, new { filterEnabled = true, filterIndex = 1, filterKind = "noise_gate_filter", filterSettings = new { } }));

            var settings = await gain.GetSettingsAsync().WithTimeoutAsync(Timeout);
            var none = await gate.GetSettingsAsync().WithTimeoutAsync(Timeout);

            Assert.Equal("""{"db":3.5}""", settings.GetRawText());
            Assert.Equal("{}", none.GetRawText());
            var sent = test.AllSent("GetSourceFilter");
            Assert.Equal(new[] { (test.Mic.Uuid, "Gain"), (test.Mic.Uuid, "Gate") }, sent.Select(data => (data.GetProperty("sourceUuid").GetGuid(), data.GetProperty("filterName").GetString()!)).ToArray());
        }

        [Fact]
        public async Task TheSettingsOfAFilter_AreChanged_AndTheCallReturnsWhenOBSStudioHasAnswered()
        {
            // The model shows no settings of a filter (OBS Studio does not announce them), so there is nothing to wait for: with a time for the events that is long, the call still returns at once, and reads nothing.
            var options = CallFixture.DefaultOptions();
            options.ConfirmationTimeout = TimeSpan.FromSeconds(30);
            await using var test = await CallFixture.StartAsync(options, world => world.AddFilter(UuidOf(world, "Mic"), "Gain", "gain_filter"));
            var gain = test.Mic.Filters["Gain"];
            test.Answer("SetSourceFilterSettings");
            var before = test.Count("GetSourceFilterList");

            await gain.SetSettingsAsync(new { db = 6 }, overlay: false).WithTimeoutAsync(Timeout);

            var sent = Assert.Single(test.AllSent("SetSourceFilterSettings"));
            Assert.Equal((test.Mic.Uuid, "Gain", false), (sent.GetProperty("sourceUuid").GetGuid(), sent.GetProperty("filterName").GetString(), sent.GetProperty("overlay").GetBoolean()));
            Assert.Equal(6, sent.GetProperty("filterSettings").GetProperty("db").GetInt32());
            Assert.Equal(before, test.Count("GetSourceFilterList"));
        }

        [Fact]
        public async Task ANullFieldOfTheSettings_AsksForNothing_AndTheCallDoesNotWaitForIt()
        {
            // OBS Studio ignores a null field (checked with OBS Studio 33.0.0): the field keeps what it was, so the call is done when the other fields are there.
            var options = CallFixture.DefaultOptions();
            options.ConfirmationTimeout = TimeSpan.FromSeconds(30);
            await using var test = await CallFixture.StartAsync(options, world => world.InputSettings[UuidOf(world, "Camera")] = new { text = "Hello", size = 4 });
            AnswerWithEvent(test, "SetInputSettings", world => world.InputSettings[test.Camera.Uuid] = new { text = "Hello", size = 5 }, "InputSettingsChanged", new { inputName = "Camera", inputUuid = test.Camera.Uuid, inputSettings = new { text = "Hello", size = 5 } }, InputEvents);

            await test.Camera.SetSettingsAsync(new Dictionary<string, object?> { ["text"] = null, ["size"] = 5 }).WithTimeoutAsync(Timeout);

            Assert.Equal("Hello", test.Camera.Settings.GetProperty("text").GetString());
            Assert.Equal(5, test.Camera.Settings.GetProperty("size").GetInt32());
        }

        [Fact]
        public async Task ANullFieldOfAReset_IsNotExpected_BecauseOBSStudioDoesNotKeepIt()
        {
            var options = CallFixture.DefaultOptions();
            options.ConfirmationTimeout = TimeSpan.FromSeconds(30);
            await using var test = await CallFixture.StartAsync(options, world => world.InputSettings[UuidOf(world, "Camera")] = new { text = "Hello", size = 4 });
            AnswerWithEvent(test, "SetInputSettings", world => world.InputSettings[test.Camera.Uuid] = new { size = 5 }, "InputSettingsChanged", new { inputName = "Camera", inputUuid = test.Camera.Uuid, inputSettings = new { size = 5 } }, InputEvents);

            await test.Camera.SetSettingsAsync(new Dictionary<string, object?> { ["text"] = null, ["size"] = 5 }, overlay: false).WithTimeoutAsync(Timeout);

            Assert.Equal("""{"size":5}""", test.Camera.Settings.GetRawText());
        }

        [Fact]
        public async Task TheSettingsOfAFilter_ThatAreNotAnObject_AreAnArgumentException()
        {
            await using var test = await CallFixture.StartAsync(setup: world => world.AddFilter(UuidOf(world, "Mic"), "Gain", "gain_filter"));
            var gain = test.Mic.Filters["Gain"];

            var number = Assert.Throws<ArgumentException>(() => { _ = gain.SetSettingsAsync(5); });

            Assert.Contains("the settings of a filter must be an object", number.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, test.Count("SetSourceFilterSettings"));
        }
    }
}
