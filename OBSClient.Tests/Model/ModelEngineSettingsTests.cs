namespace OBSStudioClient.Tests.Model
{
    using System.Text.Json;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Model;
    using OBSStudioClient.Model.Engine;

    /// <summary>
    /// What the model holds about the settings of the inputs and the filters, and about the balance, the sync offset, the monitor type and the tracks of the audio of an input: how it reads them,
    /// what the events of OBS Studio make of them, what it does with a read that was sent before an event, and what it does with a read that OBS Studio does not answer.
    /// </summary>
    public class ModelEngineSettingsTests
    {
        private const int InputEvents = 8;
        private const int FilterEvents = 32;

        private static readonly ReadKind[] AudioKinds = [ReadKind.AudioMute, ReadKind.AudioVolume, ReadKind.AudioBalance, ReadKind.AudioSyncOffset, ReadKind.AudioMonitorType, ReadKind.AudioTracks];

        // A world with a scene "Gaming" that shows the input "Mic" (which has audio) and "Camera" (which has none), and has the filter "Color"; the input "Mic" has the filters "Gain" and "Gate".
        private static EngineHarness Started(out Guid mic, out Guid camera, Action<FakeWorld, Guid, Guid>? setup = null, ModelEngineOptions? options = null)
        {
            var h = new EngineHarness(options);
            h.World.AddScene("Scene");
            var gaming = h.World.AddScene("Gaming");
            var micUuid = mic = h.World.AddInput("Mic", "wasapi_input_capture", 2);
            var cameraUuid = camera = h.World.AddInput("Camera", "dshow_input", 8);
            h.World.AddItem(gaming, micUuid, "Mic");
            h.World.AddItem(gaming, cameraUuid, "Camera");
            h.World.AddFilter(gaming, "Color", "color_filter_v2");
            h.World.AddFilter(micUuid, "Gain", "gain_filter");
            h.World.AddFilter(micUuid, "Gate", "noise_gate_filter");
            setup?.Invoke(h.World, micUuid, cameraUuid);
            h.Start();
            h.SettleWithTime();
            h.Engine.TakeChanges();
            return h;
        }

        private static SettingsValue Json(string text)
        {
            return SettingsValue.From(JsonDocument.Parse(text).RootElement);
        }

        private static EngineInput Input(EngineHarness h, Guid uuid)
        {
            return h.Engine.Snapshot().Inputs.Single(input => input.Uuid == uuid);
        }

        private static ModelChange[] Changes(EngineHarness h, ModelChangeKind kind)
        {
            return h.Engine.TakeChanges().Where(change => change.Kind == kind).ToArray();
        }

        private static ReadRequest[] NextRead(EngineHarness h)
        {
            return h.Take() is { } ticket ? ticket.Requests.ToArray() : [];
        }

        private static void SettingsChanged(EngineHarness h, Guid input, string name, object settings)
        {
            h.Event("InputSettingsChanged", new { inputName = name, inputUuid = input, inputSettings = settings }, InputEvents);
        }

        [Fact]
        public void TheSettingsAndTheAudioState_OfEveryInput_AreRead_BeforeTheInputIsAnnounced()
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            var mic = h.World.AddInput("Mic", "wasapi_input_capture", 2);
            var camera = h.World.AddInput("Camera", "dshow_input", 8);
            h.Start();

            // The lists are read first: nothing is announced before the rest is known.
            h.Complete(h.Take()!);
            Assert.Empty(h.Engine.Snapshot().Inputs);
            var read = NextRead(h);

            Assert.Equal(AudioKinds, read.Where(request => AudioKinds.Contains(request.Kind)).Select(request => request.Kind).ToArray());
            Assert.Equal(new[] { mic, camera }.Order(), read.Where(request => request.Kind == ReadKind.InputSettings).Select(request => request.Target).Order());
            Assert.Equal(
                new[]
                {
                    RequestType.GetInputMute, RequestType.GetInputVolume, RequestType.GetInputAudioBalance, RequestType.GetInputAudioSyncOffset, RequestType.GetInputAudioMonitorType, RequestType.GetInputAudioTracks,
                },
                read.Where(request => AudioKinds.Contains(request.Kind)).Select(request => request.Type).ToArray());
        }

        [Fact]
        public void TheSettingsOfAnInput_AreInTheModel_AsOBSStudioSaidThem()
        {
            var h = Started(out var mic, out var camera, (world, micUuid, cameraUuid) =>
            {
                world.InputSettings[cameraUuid] = new { width = 640, height = 360, font = new { face = "Arial", size = 48 } };
            });

            Assert.True(Json("""{"font":{"size":48,"face":"Arial"},"height":360,"width":640}""").Equals(Input(h, camera).Settings));
            Assert.Equal("{}", Input(h, mic).Settings!.ToString());
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void AnEventThatChangesTheSettings_IsAppliedAtOnce_WithoutARead()
        {
            var h = Started(out _, out var camera);

            SettingsChanged(h, camera, "Camera", new { width = 800 });

            Assert.True(Json("""{"width":800}""").Equals(Input(h, camera).Settings));
            var change = Assert.Single(Changes(h, ModelChangeKind.InputSettingsChanged));
            Assert.Equal((camera, "Camera"), (change.Subject, change.Text));
            Assert.Equal("{}", change.OldValue!.ToString());
            Assert.Equal("""{"width":800}""", change.NewValue!.ToString());
            Assert.Null(h.Take());
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void TheSameSettingsAgain_AreNoChange_WhateverTheOrderOfTheFieldsAndTheWayTheNumbersAreWritten()
        {
            var h = Started(out _, out var camera);
            h.Event("InputSettingsChanged", JsonDocument.Parse("""{"inputName":"Camera","inputUuid":"%s","inputSettings":{"width":640,"ratio":1.0,"font":{"face":"Arial","size":48}}}""".Replace("%s", camera.ToString())).RootElement, InputEvents);
            h.Engine.TakeChanges();

            h.Event("InputSettingsChanged", JsonDocument.Parse("""{"inputName":"Camera","inputUuid":"%s","inputSettings":{"font":{"size":48,"face":"Arial"},"ratio":1,"width":640}}""".Replace("%s", camera.ToString())).RootElement, InputEvents);

            Assert.Empty(h.Engine.TakeChanges());
        }

        [Fact]
        public void AReadThatWasSentBeforeASettingsEvent_DoesNotUndoIt()
        {
            var h = Started(out _, out var camera);
            h.Engine.RequestRefresh(ReadKind.InputSettings, camera, h.Now);
            var read = h.Take()!;
            Assert.Equal(camera, read.Requests.Single().Target);

            // The event arrives while the read is under way: it is newer than the read, whatever the read says.
            SettingsChanged(h, camera, "Camera", new { width = 800 });
            h.Complete(read);

            Assert.True(Json("""{"width":800}""").Equals(Input(h, camera).Settings));
        }

        [Fact]
        public void AReadThatWasSentAfterASettingsEvent_ShowsWhatCameAfterIt()
        {
            var h = Started(out _, out var camera);
            SettingsChanged(h, camera, "Camera", new { width = 800 });

            h.World.InputSettings[camera] = new { width = 900 };
            h.Engine.RequestRefresh(ReadKind.InputSettings, camera, h.Now);
            h.Settle();

            Assert.True(Json("""{"width":900}""").Equals(Input(h, camera).Settings));
            Assert.Single(Changes(h, ModelChangeKind.InputSettingsChanged), change => change.NewValue!.ToString() == """{"width":900}""");
        }

        [Fact]
        public void ARefreshOfTheSettings_ReadsThemAgain_AndIsDoneWhenTheReadHasBeenApplied()
        {
            var h = Started(out _, out var camera);

            h.Engine.RequestRefresh(ReadKind.InputSettings, camera, h.Now);

            Assert.False(h.Engine.IsRefreshed(ReadKind.InputSettings, camera));
            var read = NextRead(h);
            Assert.Equal(new[] { ReadKind.InputSettings }, read.Select(request => request.Kind).ToArray());
            Assert.Equal(camera, read.Single().Target);
        }

        [Fact]
        public void SettingsThatOBSStudioRefusesForGood_LeaveTheInputWithNone_AndTheInputIsAnnounced()
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            var camera = h.World.AddInput("Camera", "dshow_input", 8);
            h.Start();

            // A simulation that does not know the request answers 204: there are no settings to wait for.
            h.Settle(request => request.Kind == ReadKind.InputSettings ? (204, null) : h.World.Answer(request));

            Assert.True(h.Engine.IsSettled);
            Assert.Equal("{}", Input(h, camera).Settings!.ToString());
        }

        [Fact]
        public void TheSettingsOfAnInputThatOBSStudioDoesNotHave_AreReadAgain_AndTheListOfInputsDecides()
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            var camera = h.World.AddInput("Camera", "dshow_input", 8);
            h.Start();
            h.Complete(h.Take()!);

            // The read of the settings says that the input is not there: the list is read again, and says that it is.
            var settings = h.Take()!;
            h.Complete(settings, request => request.Kind == ReadKind.InputSettings ? (600, null) : h.World.Answer(request));
            Assert.Empty(h.Engine.Snapshot().Inputs);

            h.SettleWithTime();

            Assert.Equal(camera, Assert.Single(h.Engine.Snapshot().Inputs).Uuid);
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void AnEventForTheSettingsOfAnInputThatTheModelDoesNotKnow_MakesTheListOfInputsBeRead()
        {
            var h = Started(out _, out _);

            SettingsChanged(h, Guid.NewGuid(), "Unknown", new { a = 1 });

            Assert.Empty(h.Engine.TakeChanges());
            Assert.Contains(ReadKind.Inputs, NextRead(h).Select(request => request.Kind));
        }

        [Fact]
        public void ANewInput_IsAnnouncedWhenItsSettingsAreKnown()
        {
            var h = Started(out _, out _);
            var created = Guid.NewGuid();
            h.World.Inputs.Add((created, "Logo", "image_source", 8, false, 1.0));
            h.World.InputSettings[created] = new { file = "logo.png" };

            EngineEvents.InputCreated(h, created, "Logo");
            Assert.DoesNotContain(h.Engine.Snapshot().Inputs, input => input.Uuid == created);
            h.SettleWithTime();

            Assert.True(Json("""{"file":"logo.png"}""").Equals(Input(h, created).Settings));
            Assert.Single(Changes(h, ModelChangeKind.InputAdded), change => change.Subject == created);
        }

        [Fact]
        public void WhenTheConnectionIsMadeAgain_TheSettingsAreReadAgain_AndWhatChangedIsTold()
        {
            var h = Started(out _, out var camera, (world, _, cameraUuid) => world.InputSettings[cameraUuid] = new { width = 640 });
            h.End();
            h.World.InputSettings[camera] = new { width = 1280 };

            h.Start();
            h.SettleWithTime();

            Assert.True(Json("""{"width":1280}""").Equals(Input(h, camera).Settings));
            var change = Assert.Single(Changes(h, ModelChangeKind.InputSettingsChanged));
            Assert.Equal("""{"width":640}""", change.OldValue!.ToString());
        }

        [Fact]
        public void TheAudioDetails_AreReadWithTheMuteAndTheVolume_AndAreInTheModel()
        {
            var h = Started(out var mic, out var camera, (world, micUuid, _) =>
            {
                world.AudioBalances[micUuid] = 0.25f;
                world.SyncOffsets[micUuid] = 150;
                world.MonitorTypes[micUuid] = "OBS_MONITORING_TYPE_MONITOR_ONLY";
                world.Tracks[micUuid] = 0b101101;
            });

            var input = Input(h, mic);

            Assert.Equal((0.25f, 150, ObsMonitorType.MonitorOnly, 0b101101), (input.AudioBalance, input.SyncOffset, input.MonitorType, input.Tracks));
            var withoutAudio = Input(h, camera);
            Assert.Equal((0.5f, 0, ObsMonitorType.Off, 63), (withoutAudio.AudioBalance, withoutAudio.SyncOffset, withoutAudio.MonitorType, withoutAudio.Tracks));
        }

        [Fact]
        public void TheAudioDetailEvents_ChangeTheModelAtOnce_AndTheChangeSaysFromWhatToWhat()
        {
            var h = Started(out var mic, out _);

            h.Event("InputAudioBalanceChanged", new { inputName = "Mic", inputUuid = mic, inputAudioBalance = 0.75 }, InputEvents);
            h.Event("InputAudioSyncOffsetChanged", new { inputName = "Mic", inputUuid = mic, inputAudioSyncOffset = -200 }, InputEvents);
            h.Event("InputAudioMonitorTypeChanged", new { inputName = "Mic", inputUuid = mic, monitorType = "OBS_MONITORING_TYPE_MONITOR_AND_OUTPUT" }, InputEvents);
            h.Event("InputAudioTracksChanged", new { inputName = "Mic", inputUuid = mic, inputAudioTracks = new Dictionary<string, bool> { ["1"] = true, ["2"] = false, ["3"] = false, ["4"] = false, ["5"] = false, ["6"] = true } }, InputEvents);

            var input = Input(h, mic);
            Assert.Equal((0.75f, -200, ObsMonitorType.MonitorAndOutput, 0b100001), (input.AudioBalance, input.SyncOffset, input.MonitorType, input.Tracks));
            var changes = h.Engine.TakeChanges();
            Assert.Equal(
                new[] { ModelChangeKind.InputAudioBalanceChanged, ModelChangeKind.InputSyncOffsetChanged, ModelChangeKind.InputMonitorTypeChanged, ModelChangeKind.InputTracksChanged },
                changes.Select(change => change.Kind).ToArray());
            Assert.Equal((0.5f, 0.75f), ((float)changes[0].OldValue!, (float)changes[0].NewValue!));
            Assert.Equal((0, -200), ((int)changes[1].OldValue!, (int)changes[1].NewValue!));
            Assert.Equal((ObsMonitorType.Off, ObsMonitorType.MonitorAndOutput), ((ObsMonitorType)changes[2].OldValue!, (ObsMonitorType)changes[2].NewValue!));
            Assert.Equal((63, 33), ((int)changes[3].OldValue!, (int)changes[3].NewValue!));
            Assert.Null(h.Take());
        }

        [Fact]
        public void AnAudioDetailEvent_ForAnInputThatTheModelDoesNotKnow_MakesTheListOfInputsBeRead()
        {
            var h = Started(out _, out _);

            h.Event("InputAudioBalanceChanged", new { inputName = "Unknown", inputUuid = Guid.NewGuid(), inputAudioBalance = 0.75 }, InputEvents);

            Assert.Contains(ReadKind.Inputs, NextRead(h).Select(request => request.Kind));
        }

        [Fact]
        public void AnAudioDetailThatOBSStudioRefusesForGood_DoesNotKeepTheInputFromBeingAnnounced()
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            var mic = h.World.AddInput("Mic", "wasapi_input_capture", 2);
            h.World.AudioBalances[mic] = 0.25f;
            h.Start();

            // An obs-websocket that does not know the request answers 204; the details that are answered are applied.
            h.Settle(request => request.Kind == ReadKind.AudioTracks ? (204, null) : h.World.Answer(request));

            Assert.True(h.Engine.IsSettled);
            var input = Input(h, mic);
            Assert.Equal((0.25f, 63), (input.AudioBalance, input.Tracks));
        }

        [Fact]
        public void AnAudioStateOfWhichTheEndDoesNotCome_IsReadAgain_AsAWhole()
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            var mic = h.World.AddInput("Mic", "wasapi_input_capture", 2);
            h.World.SyncOffsets[mic] = 40;
            h.Start();
            h.Complete(h.Take()!);

            // The batch ends after the volume: the balance, the sync offset, the monitor type and the tracks did not come.
            var audio = h.Take()!;
            h.Complete(audio, only: audio.Requests.ToList().FindIndex(request => request.Kind == ReadKind.AudioVolume) + 1);
            Assert.Empty(h.Engine.Snapshot().Inputs);
            Assert.False(h.Engine.IsSettled);

            h.SettleWithTime();

            Assert.Equal(40, Input(h, mic).SyncOffset);
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void AnAudioDetailThatOBSStudioIsNotReadyFor_IsReadAgain()
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            var mic = h.World.AddInput("Mic", "wasapi_input_capture", 2);
            h.World.AudioBalances[mic] = 0.25f;
            h.Start();
            var refused = false;

            h.SettleWithTime(request =>
            {
                if (request.Kind == ReadKind.AudioBalance && !refused)
                {
                    refused = true;
                    return (207, null);
                }

                return h.World.Answer(request);
            });

            Assert.True(refused);
            Assert.Equal(0.25f, Input(h, mic).AudioBalance);
        }

        [Fact]
        public void AnAudioStateThatIsBiggerThanTheLimit_IsReadAlone()
        {
            var h = new EngineHarness(new ModelEngineOptions { MaxBatch = 2 });
            h.World.AddScene("Scene");
            h.World.AddInput("Mic", "wasapi_input_capture", 2);
            h.Start();

            var sizes = new List<int>();
            while (h.Take() is { } ticket)
            {
                sizes.Add(ticket.Requests.Count);
                if (ticket.Requests.Count > 2)
                {
                    Assert.Equal(AudioKinds, ticket.Requests.Select(request => request.Kind).ToArray());
                }

                h.Complete(ticket);
            }

            Assert.Contains(6, sizes);
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void AnEventForTheSettingsOfAFilter_IsLeftAlone_BecauseTheModelDoesNotHoldThem()
        {
            // The protocol has the event and OBS Studio does not send it; the model has no settings of filters to keep, so should one come it changes nothing and asks for no read.
            var h = Started(out _, out _);

            h.Event("SourceFilterSettingsChanged", new { sourceName = "Mic", filterName = "Gate", filterSettings = new { open_threshold = -30 } }, FilterEvents);

            Assert.Empty(h.Engine.TakeChanges());
            Assert.Null(h.Take());
        }
    }
}
