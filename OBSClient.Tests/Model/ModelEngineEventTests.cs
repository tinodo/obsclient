namespace OBSStudioClient.Tests.Model
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Model.Engine;

    /// <summary>
    /// What the model does with each event of OBS Studio that it listens to: for an object that it knows, for one that it does not know yet,
    /// and for one that was removed.
    /// </summary>
    public class ModelEngineEventTests
    {
        private static EngineHarness Started(Action<FakeWorld>? setup = null)
        {
            var harness = new EngineHarness();
            harness.World.AddScene("Scene");
            setup?.Invoke(harness.World);
            harness.Start();
            harness.SettleWithTime();
            harness.Engine.TakeChanges();
            return harness;
        }

        // Takes the next read and answers it from the world, and says which parts it was for; nothing when the model has nothing to read now.
        private static ReadKind[] NextRead(EngineHarness harness)
        {
            if (harness.Take() is not { } ticket)
            {
                return [];
            }

            var kinds = ticket.Requests.Select(request => request.Kind).ToArray();
            harness.Complete(ticket);
            return kinds;
        }

        [Fact]
        public void AnEventWithoutAConnection_IsIgnored()
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");

            EngineEvents.SceneCreated(h, Guid.NewGuid(), "Early");
            h.Start();
            h.SettleWithTime();
            h.End();
            EngineEvents.SceneCreated(h, Guid.NewGuid(), "Late");

            Assert.Equal(new[] { "Scene" }, h.Engine.Snapshot().Scenes.Select(scene => scene.Name).ToArray());
        }

        [Fact]
        public void AnEventOfATypeThatTheModelDoesNotUse_IsIgnored()
        {
            var h = Started();

            // The events that OBS Studio sends many times a second are not subscribed to by default, and the model does not use them.
            h.Event("InputActiveStateChanged", new { inputName = "x", inputUuid = Guid.NewGuid(), videoActive = true }, 8);

            Assert.Empty(h.Engine.TakeChanges());
            Assert.Empty(NextRead(h));
        }

        [Fact]
        public void ARenamedScene_IsRenamed_AndTheChangeSaysFromWhatToWhat()
        {
            Guid gaming = Guid.Empty;
            var h = Started(world => gaming = world.AddScene("Gaming"));

            h.Event("SceneNameChanged", new { sceneUuid = gaming, oldSceneName = "Gaming", sceneName = "Playing" }, 4);

            Assert.Contains(h.Engine.Snapshot().Scenes, scene => scene.Name == "Playing");
            var change = Assert.Single(h.Engine.TakeChanges());
            Assert.Equal((ModelChangeKind.SceneRenamed, gaming, "Gaming", "Playing"), (change.Kind, change.Subject, change.OldValue, change.NewValue));
        }

        [Fact]
        public void TheRenameOfASceneThatTheModelDoesNotKnow_MakesTheListOfScenesBeRead_AndOfOneThatWasRemoved_Nothing()
        {
            var h = Started();
            var removed = Guid.NewGuid();
            EngineEvents.SceneRemoved(h, removed, "Old");

            h.Event("SceneNameChanged", new { sceneUuid = removed, oldSceneName = "Old", sceneName = "Older" }, 4);
            Assert.Empty(NextRead(h));

            h.Event("SceneNameChanged", new { sceneUuid = Guid.NewGuid(), oldSceneName = "A", sceneName = "B" }, 4);
            Assert.Equal(new[] { ReadKind.Scenes }, NextRead(h));
        }

        [Fact]
        public void TheListOfScenes_PutsTheScenesInOrder_AndTellsWhichMoved()
        {
            Guid a = Guid.Empty;
            Guid b = Guid.Empty;
            var h = Started(world =>
            {
                b = world.AddScene("B");
                a = world.AddScene("A");
            });
            Assert.Equal(new[] { "A", "B", "Scene" }, h.Engine.Snapshot().Scenes.Select(scene => scene.Name).ToArray());
            var scene = h.Engine.Snapshot().Scenes.Single(s => s.Name == "Scene").Uuid;

            h.Event("SceneListChanged", new { scenes = new[] { new { sceneIndex = 0, sceneName = "Scene", sceneUuid = scene }, new { sceneIndex = 1, sceneName = "B", sceneUuid = b }, new { sceneIndex = 2, sceneName = "A", sceneUuid = a } } }, 4);

            Assert.Equal(new[] { "Scene", "B", "A" }, h.Engine.Snapshot().Scenes.Select(s => s.Name).ToArray());
            Assert.Equal(new[] { 0, 1, 2 }, h.Engine.Snapshot().Scenes.Select(s => s.Index).ToArray());
            // B stays where it is: the two others swap places.
            Assert.Equal(2, h.Engine.TakeChanges().Count(change => change.Kind == ModelChangeKind.SceneOrderChanged));
        }

        [Fact]
        public void AListOfScenesWithASceneThatTheModelDoesNotKnow_MakesTheListBeRead_AndOneThatWasRemovedIsLeftOut()
        {
            var h = Started();
            var removed = Guid.NewGuid();
            EngineEvents.SceneRemoved(h, removed, "Old");
            var scene = h.Engine.Snapshot().Scenes.Single().Uuid;

            h.Event("SceneListChanged", new { scenes = new[] { new { sceneIndex = 0, sceneName = "Old", sceneUuid = removed }, new { sceneIndex = 1, sceneName = "Scene", sceneUuid = scene } } }, 4);
            Assert.Empty(NextRead(h));

            h.Event("SceneListChanged", new { scenes = new[] { new { sceneIndex = 0, sceneName = "New", sceneUuid = Guid.NewGuid() }, new { sceneIndex = 1, sceneName = "Scene", sceneUuid = scene } } }, 4);
            Assert.Equal(new[] { ReadKind.Scenes }, NextRead(h));
        }

        [Fact]
        public void AGroup_IsNotInTheListOfScenes_AndDoesNotMoveTheScenes()
        {
            var h = Started();
            var group = Guid.NewGuid();

            h.Event("SceneCreated", new { sceneName = "Group", sceneUuid = group, isGroup = true }, 4);

            var snapshot = h.Engine.Snapshot();
            Assert.Equal(new[] { "Scene" }, snapshot.Scenes.Select(scene => scene.Name).ToArray());
            Assert.Equal(new[] { 0 }, snapshot.Scenes.Select(scene => scene.Index).ToArray());
            Assert.Contains(h.Engine.TakeChanges(), change => change.Kind == ModelChangeKind.SceneAdded && change.Subject == group);
            Assert.Contains(NextRead(h), kind => kind == ReadKind.Items);
        }

        [Fact]
        public void ARenamedInput_IsRenamed_AndTheRenameOfOneThatTheModelDoesNotKnow_MakesTheListOfInputsBeRead()
        {
            Guid mic = Guid.Empty;
            var h = Started(world => mic = world.AddInput("Mic"));

            h.Event("InputNameChanged", new { inputUuid = mic, oldInputName = "Mic", inputName = "Microphone" }, 8);

            Assert.Equal("Microphone", Assert.Single(h.Engine.Snapshot().Inputs).Name);
            var change = Assert.Single(h.Engine.TakeChanges());
            Assert.Equal((ModelChangeKind.InputRenamed, "Mic", "Microphone"), (change.Kind, change.OldValue, change.NewValue));

            var removed = Guid.NewGuid();
            EngineEvents.InputRemoved(h, removed, "Old");
            h.Event("InputNameChanged", new { inputUuid = removed, oldInputName = "Old", inputName = "Older" }, 8);
            Assert.Empty(NextRead(h));

            h.Event("InputNameChanged", new { inputUuid = Guid.NewGuid(), oldInputName = "A", inputName = "B" }, 8);
            Assert.Equal(new[] { ReadKind.Inputs }, NextRead(h));
        }

        [Fact]
        public void TheMuteAndTheVolumeOfAnInput_FollowTheEvents()
        {
            Guid mic = Guid.Empty;
            var h = Started(world => mic = world.AddInput("Mic", "wasapi_input_capture", 2));

            h.Event("InputMuteStateChanged", new { inputName = "Mic", inputUuid = mic, inputMuted = true }, 8);
            h.Event("InputMuteStateChanged", new { inputName = "Mic", inputUuid = mic, inputMuted = true }, 8);
            h.Event("InputVolumeChanged", new { inputName = "Mic", inputUuid = mic, inputVolumeMul = 0.5, inputVolumeDb = -6.02 }, 8);
            h.Event("InputVolumeChanged", new { inputName = "Mic", inputUuid = mic, inputVolumeMul = 0.5, inputVolumeDb = -6.02 }, 8);

            var input = Assert.Single(h.Engine.Snapshot().Inputs);
            Assert.True(input.Muted);
            Assert.Equal(0.5f, input.VolumeMul, 3);
            Assert.Equal(-6.02f, input.VolumeDb, 3);
            var changes = h.Engine.TakeChanges();
            Assert.Equal(new[] { ModelChangeKind.InputMuteChanged, ModelChangeKind.InputVolumeChanged }, changes.Select(change => change.Kind).ToArray());
        }

        [Fact]
        public void TheMuteAndTheVolumeOfAnInputThatTheModelDoesNotKnow_MakeTheListOfInputsBeRead_UnlessItWasRemoved()
        {
            var h = Started();
            var removed = Guid.NewGuid();
            EngineEvents.InputRemoved(h, removed, "Old");

            h.Event("InputMuteStateChanged", new { inputName = "Old", inputUuid = removed, inputMuted = true }, 8);
            h.Event("InputVolumeChanged", new { inputName = "Old", inputUuid = removed, inputVolumeMul = 0.5, inputVolumeDb = -6.0 }, 8);
            Assert.Empty(NextRead(h));

            h.Event("InputMuteStateChanged", new { inputName = "New", inputUuid = Guid.NewGuid(), inputMuted = true }, 8);
            Assert.Equal(new[] { ReadKind.Inputs }, NextRead(h));
            h.Event("InputVolumeChanged", new { inputName = "New", inputUuid = Guid.NewGuid(), inputVolumeMul = 0.5, inputVolumeDb = -6.0 }, 8);
            Assert.Equal(new[] { ReadKind.Inputs }, NextRead(h));
        }

        [Fact]
        public void StudioMode_FollowsItsEvent_AndAnEventThatChangesNothingTellsNothing()
        {
            var h = Started();

            h.Event("StudioModeStateChanged", new { studioModeEnabled = true }, 1 << 10);
            h.Event("StudioModeStateChanged", new { studioModeEnabled = true }, 1 << 10);

            Assert.True(h.Engine.Snapshot().StudioMode);
            var change = Assert.Single(h.Engine.TakeChanges());
            Assert.Equal((ModelChangeKind.StudioModeChanged, false, true), (change.Kind, change.OldValue, change.NewValue));

            h.Event("StudioModeStateChanged", new { studioModeEnabled = false }, 1 << 10);
            Assert.False(h.Engine.Snapshot().StudioMode);
        }

        [Fact]
        public void TheProgramSceneIsRead_WhenItsEventComes_AndTheEventIsNotTrusted()
        {
            Guid other = Guid.Empty;
            var h = Started(world => other = world.AddScene("Other"));
            h.World.Program = other;

            // The payload is for the end of the transition; what the program scene is now is what OBS says when asked.
            h.Event("CurrentProgramSceneChanged", new { sceneName = "Scene", sceneUuid = h.Engine.Snapshot().Scenes.Single(scene => scene.Name == "Scene").Uuid }, 4);
            Assert.Equal(new[] { ReadKind.Program }, NextRead(h));
            h.SettleWithTime();

            Assert.Equal(other, h.Engine.Snapshot().ProgramSceneUuid);
        }

        [Fact]
        public void TheEventsOfSceneItems_OfASceneOrAnItemThatTheModelDoesNotKnow_MakeTheRightPartBeRead_UnlessTheyWereRemoved()
        {
            Guid gaming = Guid.Empty;
            Guid mic = Guid.Empty;
            var h = Started(world =>
            {
                gaming = world.AddScene("Gaming");
                mic = world.AddInput("Mic");
                world.AddItem(gaming, mic, "Mic");
            });
            var unknownScene = Guid.NewGuid();

            // A scene that the model does not know: the list of scenes is read.
            h.Event("SceneItemEnableStateChanged", new { sceneName = "X", sceneUuid = unknownScene, sceneItemId = 1, sceneItemEnabled = false }, 128);
            Assert.Equal(new[] { ReadKind.Scenes }, NextRead(h));
            h.Event("SceneItemListReindexed", new { sceneName = "X", sceneUuid = unknownScene, sceneItems = new[] { new { sceneItemId = 1, sceneItemIndex = 0 } } }, 128);
            Assert.Equal(new[] { ReadKind.Scenes }, NextRead(h));
            EngineEvents.ItemCreated(h, unknownScene, "X", 1, 0, mic, "Mic");
            Assert.Equal(new[] { ReadKind.Scenes }, NextRead(h));

            // An item that the model does not know in a scene that it does: the items of the scene are read.
            h.Event("SceneItemLockStateChanged", new { sceneName = "Gaming", sceneUuid = gaming, sceneItemId = 77, sceneItemLocked = true }, 128);
            Assert.Equal(new[] { ReadKind.Items }, NextRead(h));
            h.Event("SceneItemListReindexed", new { sceneName = "Gaming", sceneUuid = gaming, sceneItems = new[] { new { sceneItemId = 1, sceneItemIndex = 0 }, new { sceneItemId = 78, sceneItemIndex = 1 } } }, 128);
            Assert.Equal(new[] { ReadKind.Items }, NextRead(h));

            // Removed: nothing is read for what is gone.
            EngineEvents.SceneRemoved(h, gaming, "Gaming");
            h.World.Scenes.RemoveAll(scene => scene.Uuid == gaming);
            h.World.Items.Remove(gaming);
            h.Event("SceneItemEnableStateChanged", new { sceneName = "Gaming", sceneUuid = gaming, sceneItemId = 1, sceneItemEnabled = false }, 128);
            h.Event("SceneItemListReindexed", new { sceneName = "Gaming", sceneUuid = gaming, sceneItems = new[] { new { sceneItemId = 1, sceneItemIndex = 0 } } }, 128);
            EngineEvents.ItemCreated(h, gaming, "Gaming", 5, 0, mic, "Mic");
            Assert.DoesNotContain(NextRead(h), kind => kind == ReadKind.Scenes);
        }

        [Fact]
        public void AnItemThatWasRemovedBeforeTheModelKnewIt_IsNotBroughtBack_ByAReadThatWasSentBeforeTheRemoval()
        {
            Guid gaming = Guid.Empty;
            Guid mic = Guid.Empty;
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            gaming = h.World.AddScene("Gaming");
            mic = h.World.AddInput("Mic");
            h.World.AddItem(gaming, mic, "Mic");
            h.Start();

            // The removal comes while the model has not read the scene items: the read that follows still has the item, for it was sent before.
            EngineEvents.ItemRemoved(h, gaming, "Gaming", 1, mic, "Mic");
            h.SettleWithTime();

            Assert.Empty(h.Engine.Snapshot().Scenes.Single(scene => scene.Name == "Gaming").Items);
        }

        [Fact]
        public void AnItemThatIsMovedByTheEvent_HasTheIndexesOfTheEventAndTheItemsAreNormalizedByARead()
        {
            Guid gaming = Guid.Empty;
            var h = Started(world =>
            {
                gaming = world.AddScene("Gaming");
                var a = world.AddInput("A");
                var b = world.AddInput("B");
                world.AddItem(gaming, a, "A");
                world.AddItem(gaming, b, "B");
            });

            // The index of an item that is not 0..n-1 is a sign of a missed change: the scene is read again.
            h.Event("SceneItemListReindexed", new { sceneName = "Gaming", sceneUuid = gaming, sceneItems = new[] { new { sceneItemId = 1, sceneItemIndex = 5 }, new { sceneItemId = 2, sceneItemIndex = 6 } } }, 128);
            Assert.Equal(new[] { ReadKind.Items }, NextRead(h));
            h.SettleWithTime();

            Assert.Equal(new[] { 0, 1 }, h.Engine.Snapshot().Scenes.Single(scene => scene.Name == "Gaming").Items.Select(item => item.Index).ToArray());
        }

        [Fact]
        public void TheEventsThatTheHostPassesToTheModel_AreTheOnesThatItActsOn()
        {
            // A change of this list is a decision: the host does not pass an event that is not on it, so one that is missing is lost for the model, and one
            // that need not be here costs a message for nothing. The list is also the table that the engine dispatches from, so it cannot differ from what it does.
            EventType[] needed =
            [
                EventType.SceneCreated, EventType.SceneRemoved, EventType.SceneNameChanged, EventType.SceneListChanged, EventType.CurrentProgramSceneChanged,
                EventType.InputCreated, EventType.InputRemoved, EventType.InputNameChanged, EventType.InputMuteStateChanged, EventType.InputVolumeChanged,
                EventType.InputSettingsChanged, EventType.InputAudioBalanceChanged, EventType.InputAudioSyncOffsetChanged, EventType.InputAudioTracksChanged, EventType.InputAudioMonitorTypeChanged,
                EventType.MediaInputPlaybackStarted, EventType.MediaInputPlaybackEnded, EventType.MediaInputActionTriggered,
                EventType.SceneItemCreated, EventType.SceneItemRemoved, EventType.SceneItemListReindexed, EventType.SceneItemEnableStateChanged,
                EventType.SceneItemLockStateChanged, EventType.StudioModeStateChanged,
                EventType.StreamStateChanged, EventType.RecordStateChanged, EventType.VirtualcamStateChanged, EventType.ReplayBufferStateChanged,
                EventType.SourceFilterCreated, EventType.SourceFilterRemoved, EventType.SourceFilterNameChanged, EventType.SourceFilterEnableStateChanged, EventType.SourceFilterListReindexed,
                EventType.CurrentSceneCollectionChanging, EventType.CurrentSceneCollectionChanged,

                // What happened and is no state of the model: the model tells the application, in the order of arrival (ModelChangeKind.Notification).
                EventType.RecordFileChanged, EventType.ReplayBufferSaved, EventType.ScreenshotSaved, EventType.ExitStarted, EventType.CustomEvent, EventType.VendorEvent,
            ];

            var handled = Enum.GetValues<EventType>().Where(ModelEngine.Handles).Order().ToArray();

            Assert.Equal(needed.Order().ToArray(), handled);
        }

        [Fact]
        public void EveryEventThatTheHostPassesToTheModel_IsActedOn()
        {
            // For each of them, an event of that type that matches what the model holds changes it, or makes it read: none is passed to the model for nothing.
            var scene = Guid.NewGuid();
            var other = Guid.NewGuid();
            var mic = Guid.NewGuid();
            var camera = Guid.NewGuid();
            var clip = Guid.NewGuid();
            var cases = new (EventType Type, Action<EngineHarness> Send)[]
            {
                (EventType.SceneCreated, h => EngineEvents.SceneCreated(h, other, "Other")),
                (EventType.SceneRemoved, h => h.Event("SceneRemoved", new { sceneName = "Scene", sceneUuid = scene, isGroup = false }, 4)),
                (EventType.SceneNameChanged, h => h.Event("SceneNameChanged", new { sceneUuid = scene, oldSceneName = "Scene", sceneName = "Renamed" }, 4)),
                (EventType.SceneListChanged, h => h.Event("SceneListChanged", new { scenes = new[] { new { sceneName = "Scene", sceneUuid = scene, sceneIndex = 0 }, new { sceneName = "Other", sceneUuid = other, sceneIndex = 1 } } }, 4)),
                (EventType.CurrentProgramSceneChanged, h => h.Event("CurrentProgramSceneChanged", new { sceneName = "Scene", sceneUuid = scene }, 4)),
                (EventType.InputCreated, h => h.Event("InputCreated", new { inputName = "New", inputUuid = Guid.NewGuid(), inputKind = "color_source_v3", unversionedInputKind = "color_source", inputSettings = new { }, defaultInputSettings = new { } }, 8)),
                (EventType.InputRemoved, h => h.Event("InputRemoved", new { inputName = "Mic", inputUuid = mic }, 8)),
                (EventType.InputNameChanged, h => h.Event("InputNameChanged", new { inputUuid = mic, oldInputName = "Mic", inputName = "Microphone" }, 8)),
                (EventType.InputMuteStateChanged, h => h.Event("InputMuteStateChanged", new { inputName = "Mic", inputUuid = mic, inputMuted = true }, 8)),
                (EventType.InputVolumeChanged, h => h.Event("InputVolumeChanged", new { inputName = "Mic", inputUuid = mic, inputVolumeMul = 0.5, inputVolumeDb = -6.02 }, 8)),
                (EventType.InputSettingsChanged, h => h.Event("InputSettingsChanged", new { inputName = "Mic", inputUuid = mic, inputSettings = new { device_id = "default" } }, 8)),
                (EventType.InputAudioBalanceChanged, h => h.Event("InputAudioBalanceChanged", new { inputName = "Mic", inputUuid = mic, inputAudioBalance = 0.25 }, 8)),
                (EventType.InputAudioSyncOffsetChanged, h => h.Event("InputAudioSyncOffsetChanged", new { inputName = "Mic", inputUuid = mic, inputAudioSyncOffset = 150 }, 8)),
                (EventType.InputAudioTracksChanged, h => h.Event("InputAudioTracksChanged", new { inputName = "Mic", inputUuid = mic, inputAudioTracks = new Dictionary<string, bool> { ["1"] = true, ["2"] = false, ["3"] = true, ["4"] = true, ["5"] = true, ["6"] = true } }, 8)),
                (EventType.InputAudioMonitorTypeChanged, h => h.Event("InputAudioMonitorTypeChanged", new { inputName = "Mic", inputUuid = mic, monitorType = "OBS_MONITORING_TYPE_MONITOR_ONLY" }, 8)),
                (EventType.MediaInputPlaybackStarted, h => h.Event("MediaInputPlaybackStarted", new { inputName = "Clip", inputUuid = clip }, 256)),
                (EventType.MediaInputPlaybackEnded, h => h.Event("MediaInputPlaybackEnded", new { inputName = "Clip", inputUuid = clip }, 256)),
                (EventType.MediaInputActionTriggered, h => h.Event("MediaInputActionTriggered", new { inputName = "Clip", inputUuid = clip, mediaAction = "OBS_WEBSOCKET_MEDIA_INPUT_ACTION_PAUSE" }, 256)),
                (EventType.SceneItemCreated, h => h.Event("SceneItemCreated", new { sceneName = "Scene", sceneUuid = scene, sourceName = "Camera", sourceUuid = camera, sceneItemId = 9, sceneItemIndex = 1 }, 128)),
                (EventType.SceneItemRemoved, h => h.Event("SceneItemRemoved", new { sceneName = "Scene", sceneUuid = scene, sourceName = "Camera", sourceUuid = camera, sceneItemId = 1 }, 128)),
                (EventType.SceneItemListReindexed, h => h.Event("SceneItemListReindexed", new { sceneName = "Scene", sceneUuid = scene, sceneItems = new[] { new { sceneItemId = 1, sceneItemIndex = 7 } } }, 128)),
                (EventType.SceneItemEnableStateChanged, h => h.Event("SceneItemEnableStateChanged", new { sceneName = "Scene", sceneUuid = scene, sceneItemId = 1, sceneItemEnabled = false }, 128)),
                (EventType.SceneItemLockStateChanged, h => h.Event("SceneItemLockStateChanged", new { sceneName = "Scene", sceneUuid = scene, sceneItemId = 1, sceneItemLocked = true }, 128)),
                (EventType.StudioModeStateChanged, h => h.Event("StudioModeStateChanged", new { studioModeEnabled = true }, 1024)),
                (EventType.StreamStateChanged, h => h.Event("StreamStateChanged", new { outputActive = true, outputState = "OBS_WEBSOCKET_OUTPUT_STARTED" }, 64)),
                (EventType.RecordStateChanged, h => h.Event("RecordStateChanged", new { outputActive = true, outputState = "OBS_WEBSOCKET_OUTPUT_STARTED", outputPath = "C:\\Videos\\a.mkv" }, 64)),
                (EventType.VirtualcamStateChanged, h => h.Event("VirtualcamStateChanged", new { outputActive = true, outputState = "OBS_WEBSOCKET_OUTPUT_STARTED" }, 64)),
                (EventType.ReplayBufferStateChanged, h => h.Event("ReplayBufferStateChanged", new { outputActive = true, outputState = "OBS_WEBSOCKET_OUTPUT_STARTED" }, 64)),
                (EventType.SourceFilterCreated, h => h.Event("SourceFilterCreated", new { sourceName = "Scene", filterName = "Sharpen", filterKind = "sharpness_filter_v2", filterIndex = 1, filterSettings = new { }, defaultFilterSettings = new { } }, 32)),
                (EventType.SourceFilterRemoved, h => h.Event("SourceFilterRemoved", new { sourceName = "Scene", filterName = "Blur" }, 32)),
                (EventType.SourceFilterNameChanged, h => h.Event("SourceFilterNameChanged", new { sourceName = "Scene", oldFilterName = "Blur", filterName = "Blurred" }, 32)),
                (EventType.SourceFilterEnableStateChanged, h => h.Event("SourceFilterEnableStateChanged", new { sourceName = "Scene", filterName = "Blur", filterEnabled = false }, 32)),
                (EventType.SourceFilterListReindexed, h => h.Event("SourceFilterListReindexed", new { sourceName = "Scene", filters = new[] { new { filterEnabled = true, filterIndex = 1, filterKind = "color_filter_v2", filterName = "Blur", filterSettings = new { } } } }, 32)),
                (EventType.CurrentSceneCollectionChanging, h => h.Event("CurrentSceneCollectionChanging", new { sceneCollectionName = "Untitled" }, 2)),
                (EventType.CurrentSceneCollectionChanged, h => h.Event("CurrentSceneCollectionChanged", new { sceneCollectionName = "Other" }, 2)),
                (EventType.RecordFileChanged, h => h.Event("RecordFileChanged", new { newOutputPath = "C:\\Videos\\b.mkv" }, 64)),
                (EventType.ReplayBufferSaved, h => h.Event("ReplayBufferSaved", new { savedReplayPath = "C:\\Videos\\replay.mkv" }, 64)),
                (EventType.ScreenshotSaved, h => h.Event("ScreenshotSaved", new { savedScreenshotPath = "C:\\Pictures\\shot.png" }, 1)),
                (EventType.ExitStarted, h => h.Event("ExitStarted", new { }, 1)),
                (EventType.CustomEvent, h => h.Event("CustomEvent", new { source = "a test" }, 1)),
                (EventType.VendorEvent, h => h.Event("VendorEvent", new { vendorName = "a-plugin", eventType = "Something", eventData = new { } }, 512)),
            };

            Assert.Equal(cases.Select(c => c.Type).Order(), Enum.GetValues<EventType>().Where(ModelEngine.Handles).Order());
            foreach (var (type, send) in cases)
            {
                var h = Started(world =>
                {
                    world.Scenes.Clear();
                    world.Scenes.Add((scene, "Scene"));
                    world.Inputs.Add((mic, "Mic", "wasapi_input_capture", 2, false, 1.0));
                    world.Inputs.Add((camera, "Camera", "dshow_input", 8, false, 1.0));
                    world.Inputs.Add((clip, "Clip", "ffmpeg_source", 8327, false, 1.0));
                    world.Items[scene] = [(1, camera, false, "Camera", 0, true, false)];
                    world.AddFilter(scene, "Blur");
                    world.AddFilter(scene, "Tint");
                    world.Program = scene;
                });

                var epoch = h.Engine.Epoch;
                send(h);

                // The two events of a switch change no value: they stop the model from reading, or make it forget the collection that it held.
                var acted = h.Engine.TakeChanges().Any() || NextRead(h).Length > 0 || h.Engine.IsSwitching || h.Engine.Epoch != epoch;
                Assert.True(acted, $"An event of type {type} that the host passes to the model changed nothing and caused no read.");
            }
        }

        [Fact]
        public void AnInputThatAnEventMade_WithoutItsCapabilities_IsAnnouncedWhenTheListHasThem()
        {
            var h = Started();
            var mic = h.World.AddInput("Mic", "wasapi_input_capture", 2);

            // The capabilities are not in the description of the event in the protocol: when they are not there the list of inputs has them.
            h.Event("InputCreated", new { inputName = "Mic", inputUuid = mic, inputKind = "wasapi_input_capture", unversionedInputKind = "wasapi_input_capture", inputSettings = new { }, defaultInputSettings = new { } }, 8);
            Assert.Empty(h.Engine.Snapshot().Inputs);
            h.SettleWithTime();

            var input = Assert.Single(h.Engine.Snapshot().Inputs);
            Assert.True(input.HasAudio);
            Assert.Contains(h.Engine.TakeChanges(), change => change.Kind == ModelChangeKind.InputAdded && change.Subject == mic);
        }
    }
}
