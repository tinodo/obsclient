namespace OBSStudioClient.Tests.Simulation
{
    using System.Text.Json.Nodes;
    using static OBSStudioClient.Tests.Simulation.SimJson;

    /// <summary>
    /// The state of a simulated OBS Studio, and the changes that can be made to it. A change checks everything that OBS Studio checks before
    /// it changes anything (and throws <see cref="SimFailure"/> when OBS Studio would refuse), and then makes it and emits the events that
    /// OBS Studio emits, in the order in which it emits them. The requests of a client and the changes of <see cref="SimExternal"/> both go
    /// through here.
    /// </summary>
    internal sealed partial class SimWorld
    {
        /// <summary>The UUID of the main canvas, as a real OBS Studio reports it.</summary>
        public const string MainCanvasUuid = "6c69626f-6273-4c00-9d88-c5136d61696e";

        /// <summary>The name of the main canvas.</summary>
        public const string MainCanvasName = "Main";

        /// <summary>The name of the scene collection that OBS Studio starts with.</summary>
        public const string DefaultCollectionName = "Untitled";

        private const string FadeName = "Fade";
        private const string FadeUuid = "30a9a58c-7ffb-4c38-a170-ad8377ccf2fa";
        private const string CutName = "Cut";
        private const string CutUuid = "bb5482e7-3a9a-4c29-93e0-1a2fa40ff101";

        private readonly SimObs _obs;
        private readonly Dictionary<string, SimSource> _byUuid = new(StringComparer.Ordinal);

        // The scenes of the list of scenes, newest first: index 0 of the list is the scene that was created last (fact 24).
        private readonly List<SimScene> _liveScenes = [];

        // What the user interface lists: the live scenes, and the removed ones until their lag has run out (fact 4). Newest first.
        private readonly List<SimScene> _uiList = [];

        // The scenes that were removed and are still alive.
        private readonly List<SimScene> _detached = [];

        // The inputs that are alive, in the order in which they were created. This includes the ones that were removed and are alive.
        private readonly List<SimInput> _inputs = [];

        private readonly HashSet<string> _announcedInputs = new(StringComparer.Ordinal);
        private readonly HashSet<string> _announcedScenes = new(StringComparer.Ordinal);
        private readonly List<string> _violations = [];

        private SimScene _program;
        private Transition? _transition;
        private long _serial;

        /// <summary>Initializes a new instance of the <see cref="SimWorld"/> class: a new scene collection, which has one scene (the one that OBS Studio makes).</summary>
        /// <param name="obs">The simulated OBS Studio that the collection is in.</param>
        /// <param name="name">The name of the scene collection.</param>
        public SimWorld(SimObs obs, string name = DefaultCollectionName)
        {
            _obs = obs;
            Name = name;

            // A collection starts with one scene, and it is the program scene (fact 24: "the original Scene").
            var scene = new SimScene(obs.Random.NextGuid(), "Scene", NextSerial());
            _byUuid[scene.UuidText] = scene;
            _liveScenes.Add(scene);
            _uiList.Add(scene);
            _program = scene;
        }

        /// <summary>Gets the name of the scene collection.</summary>
        public string Name { get; }

        /// <summary>Gets the scenes of the list of scenes, in the order of the list.</summary>
        public IReadOnlyList<SimScene> LiveScenes => _liveScenes;

        /// <summary>Gets the scenes that were removed and are still alive, in the order of their creation.</summary>
        public IReadOnlyList<SimScene> DetachedScenes => _detached.OrderBy(scene => scene.Serial).ToArray();

        /// <summary>Gets the inputs that are alive, in the order of their creation.</summary>
        public IReadOnlyList<SimInput> Inputs => _inputs;

        /// <summary>Gets the program scene.</summary>
        public SimScene Program => _program;

        /// <summary>Gets what is wrong with the way that the simulation emitted its events, found while it did.</summary>
        public IReadOnlyList<string> Violations => _violations;

        private TimeSpan Now => _obs.Clock.Now;

        /// <summary>Finds a source that is alive by its UUID: a scene (also one that was removed) or an input.</summary>
        public SimSource? FindByUuid(string uuid)
        {
            return _byUuid.TryGetValue(uuid, out var source) ? source : null;
        }

        /// <summary>Finds a scene of the list of scenes by its name. A scene that was removed is not found.</summary>
        public SimScene? FindLiveScene(string name)
        {
            return _liveScenes.FirstOrDefault(scene => string.Equals(scene.Name, name, StringComparison.Ordinal));
        }

        /// <summary>Finds an input that is alive by its name. An input that was removed but is alive is found: its name stays taken (fact 5).</summary>
        public SimInput? FindInput(string name)
        {
            return _inputs.FirstOrDefault(input => string.Equals(input.Name, name, StringComparison.Ordinal));
        }

        /// <summary>
        /// Finds a source by its name, the way a request by name does: an input first, and then a scene of the list.
        /// </summary>
        /// <remarks>
        /// ASSUMPTION: when an input and a scene have the same name (a scene can take the name of an input, not the other way round: fact 13),
        /// obs-websocket finds the input, because obs_get_source_by_name finds the source that holds the name for the longest time, and that
        /// is always the input. A request that needs a scene then fails with 602. A measurement of GetSceneItemList by sceneName for a scene
        /// that has the name of an input would confirm it.
        /// </remarks>
        public SimSource? FindByName(string name)
        {
            return (SimSource?)FindInput(name) ?? FindLiveScene(name);
        }

        /// <summary>Creates a scene (CreateScene).</summary>
        public SimScene CreateScene(string name)
        {
            if (FindLiveScene(name) is not null)
            {
                throw SimFailure.Exists("A source already exists by that scene name.");
            }

            var scene = new SimScene(_obs.Random.NextGuid(), name, NextSerial())
            {
                CreatedAt = Now,
                Lag = _obs.Quirks.SceneListLag.Sample(_obs.Random),
            };
            _byUuid[scene.UuidText] = scene;
            _liveScenes.Insert(0, scene);
            _uiList.Insert(0, scene);
            if (scene.Lag > TimeSpan.Zero)
            {
                _obs.AddDeadline(Now + scene.Lag);
            }

            Emit("SceneCreated", SceneCreatedData(scene));
            Emit("SceneListChanged", Obj(("scenes", SceneListJson(forEvent: true))));
            if (_obs.Quirks.DuplicateSceneCreated)
            {
                Emit("SceneCreated", SceneCreatedData(scene));
            }

            return scene;
        }

        /// <summary>
        /// Removes a scene (RemoveScene): it leaves the list of scenes and its name is free at once. It is destroyed if nothing refers to it,
        /// else it stays alive, detached from the list (fact 7).
        /// </summary>
        public void RemoveScene(SimScene scene)
        {
            if (scene.Removed)
            {
                // ASSUMPTION: the brief asks for 600 for a scene that was removed already. A real obs-websocket 5.7.6 reads the canvas of the
                // scene, which a removed scene no longer has, and answers 701 ("Error getting canvas for the specified scene.").
                throw SimFailure.NotFound($"No source was found by the UUID of `{scene.UuidText}`.");
            }

            if (_liveScenes.Count < 2)
            {
                throw new SimFailure(SimStatus.NotEnoughResources, "You cannot remove the last scene in the collection.");
            }

            scene.Removed = true;
            scene.RemovedAt = Now;
            _liveScenes.Remove(scene);
            if (scene.Lag > TimeSpan.Zero)
            {
                _obs.AddDeadline(Now + scene.Lag);
            }

            var removedEvent = Emit("SceneRemoved", SceneRemovedData(scene));

            if (ReferenceEquals(_program, scene))
            {
                // Fact 26: OBS Studio switches to the scene that is first in the list now, at once and without a transition.
                // ASSUMPTION: this is the rule of the brief. The measurement removed the scene at index 0, and which scene follows when the
                // program scene is in the middle of the list was not measured.
                // ASSUMPTION: a transition that is running ends at once and announces its end (SceneTransitionEnded), as a second switch
                // does (fact 25). Removing the program scene during a transition was not measured.
                CancelTransition();
                _program = _liveScenes[0];
                Emit("CurrentProgramSceneChanged", ProgramSceneData());
            }

            if (scene.References == 0)
            {
                DestroyScene(scene, removedEvent, _obs.Quirks.DestroyCascadeDelay);
            }
            else
            {
                _detached.Add(scene);
            }

            Emit("SceneListChanged", Obj(("scenes", SceneListJson(forEvent: true))));
        }

        /// <summary>Renames a scene (SetSceneName).</summary>
        public void RenameScene(SimScene scene, string newName)
        {
            if (scene.Removed)
            {
                if (_obs.Quirks.HangOnRemovedSceneRename)
                {
                    throw new SimHang("A real OBS Studio never answers SetSceneName for a scene that was removed (fact 9).");
                }

                // ASSUMPTION: without the hang, the rename of a scene that was removed works, and is not announced: obs-websocket only
                // announces what happens to scenes of the main canvas, and a removed scene is no longer in it.
                if (FindLiveScene(newName) is not null)
                {
                    throw SimFailure.Exists("A source already exists by that new scene name.");
                }

                scene.Name = newName;
                return;
            }

            if (FindLiveScene(newName) is not null)
            {
                throw SimFailure.Exists("A source already exists by that new scene name.");
            }

            var oldName = scene.Name;
            scene.Name = newName;
            Emit("SceneNameChanged", Obj(("oldSceneName", oldName), ("sceneName", newName), ("sceneUuid", scene.UuidText)));
        }

        /// <summary>Creates an input, with a scene item for it in a scene (CreateInput).</summary>
        public SimInput CreateInput(SimScene scene, string name, SimInputKind kind, JsonObject? settings, bool enabled, out SimItem item)
        {
            EnsureInputNameFree(name, "A source already exists by that input name.");
            var input = NewInput(name, kind, settings);
            Emit("InputCreated", InputCreatedData(input));
            item = AddItemCore(scene, input, enabled);
            return input;
        }

        /// <summary>Creates an input that is pinned and has no scene item, as the global audio devices of OBS Studio are.</summary>
        public SimInput CreateGlobalInput(string name, SimInputKind kind)
        {
            EnsureInputNameFree(name, "A source already exists by that input name.");
            var input = NewInput(name, kind, null);
            input.Pins = 1;
            Emit("InputCreated", InputCreatedData(input));
            return input;
        }

        /// <summary>
        /// Lets go of the pin of an input, as the settings of OBS Studio do when a global audio device is set to disabled. The input is
        /// destroyed when nothing else refers to it.
        /// </summary>
        public void ReleasePin(SimInput input)
        {
            if (input.Pins == 0)
            {
                throw new SimFailure(SimStatus.InvalidResourceState, "The input is not pinned.");
            }

            input.Pins--;
            if (input.References == 0 && input.Pins == 0)
            {
                // ASSUMPTION: an input that loses its last pin is destroyed and announced with InputRemoved, as one that loses its last scene
                // item is (fact 6). The settings of OBS Studio release a global audio device without obs_source_remove, which may mean that
                // no InputRemoved is sent at all. Disabling Desktop Audio in the settings of a real OBS Studio, with obs-websocket connected,
                // would tell.
                DestroyInput(input, -1, TimeSpan.Zero);
            }
        }

        /// <summary>
        /// Removes an input (RemoveInput): it is announced once, and stays in the list of inputs, with its name taken and its scene items in
        /// place, until nothing refers to it any more (fact 5). A second removal of the same input does nothing.
        /// </summary>
        public void RemoveInput(SimInput input)
        {
            if (input.Removed)
            {
                // ASSUMPTION: obs_source_remove does nothing for a source that was removed already, so the request succeeds and nothing is
                // announced (the libobs source says so; it was not measured).
                return;
            }

            input.Removed = true;
            Emit("InputRemoved", InputRemovedData(input));
            if (input.References == 0 && input.Pins == 0)
            {
                DestroyInput(input, -1, TimeSpan.Zero);
            }
        }

        /// <summary>Renames an input (SetInputName). An input that was removed but is alive can be renamed.</summary>
        public void RenameInput(SimInput input, string newName)
        {
            EnsureInputNameFree(newName, "A source already exists by that new input name.");
            var oldName = input.Name;
            input.Name = newName;
            Emit("InputNameChanged", Obj(("inputName", newName), ("inputUuid", input.UuidText), ("oldInputName", oldName)));
        }

        /// <summary>Puts a source in a scene as a new item, on top (CreateSceneItem).</summary>
        public SimItem AddItem(SimScene scene, SimSource source, bool enabled)
        {
            if (ReferenceEquals(scene, source))
            {
                throw new SimFailure(SimStatus.CannotAct, "You cannot create scene item of a scene within itself.");
            }

            // libobs refuses a source that would make a loop of scenes that hold each other.
            // ASSUMPTION: the status for such a loop is 700 ("Failed to create the scene item."), which is what obs-websocket answers when
            // obs_scene_add fails; the protocol lists no status for it. Adding a scene to a scene that it contains, in a real OBS Studio,
            // would confirm it.
            if (source is SimScene nested && Reaches(nested, scene))
            {
                throw new SimFailure(SimStatus.ResourceCreationFailed, "Failed to create the scene item.");
            }

            return AddItemCore(scene, source, enabled);
        }

        /// <summary>
        /// Removes a scene item (RemoveSceneItem). The items above it move down by one, and no <c>SceneItemListReindexed</c> says so (fact 20).
        /// The source loses a reference, and is destroyed when that was the last (facts 6 and 7).
        /// </summary>
        public void RemoveItem(SimItem item)
        {
            var scene = item.Scene;
            scene.Items.Remove(item);
            Renumber(scene);
            if (scene.IsLive)
            {
                Emit("SceneItemRemoved", Obj(
                    ("sceneItemId", item.Id),
                    ("sceneName", scene.Name),
                    ("sceneUuid", scene.UuidText),
                    ("sourceName", item.Source.Name),
                    ("sourceUuid", item.Source.UuidText)));
            }

            ReleaseSource(item.Source, -1, TimeSpan.Zero);
        }

        /// <summary>Enables or disables a scene item (SetSceneItemEnabled). Nothing is announced when it does not change (ASSUMPTION: not measured).</summary>
        public void SetItemEnabled(SimItem item, bool enabled)
        {
            if (item.Enabled == enabled)
            {
                return;
            }

            item.Enabled = enabled;
            if (item.Scene.IsLive)
            {
                Emit("SceneItemEnableStateChanged", Obj(
                    ("sceneItemEnabled", enabled),
                    ("sceneItemId", item.Id),
                    ("sceneName", item.Scene.Name),
                    ("sceneUuid", item.Scene.UuidText)));
            }
        }

        /// <summary>Locks or unlocks a scene item (SetSceneItemLocked). Nothing is announced when it does not change (ASSUMPTION: not measured).</summary>
        public void SetItemLocked(SimItem item, bool locked)
        {
            if (item.Locked == locked)
            {
                return;
            }

            item.Locked = locked;
            if (item.Scene.IsLive)
            {
                Emit("SceneItemLockStateChanged", Obj(
                    ("sceneItemId", item.Id),
                    ("sceneItemLocked", locked),
                    ("sceneName", item.Scene.Name),
                    ("sceneUuid", item.Scene.UuidText)));
            }
        }

        /// <summary>
        /// Moves a scene item to an index (SetSceneItemIndex), which is clamped to the items of the scene. One <c>SceneItemListReindexed</c> lists
        /// every item of the scene, also when nothing moved (fact 21).
        /// </summary>
        public void SetItemIndex(SimItem item, int index)
        {
            var scene = item.Scene;
            var target = Math.Clamp(index, 0, scene.Items.Count - 1);
            scene.Items.Remove(item);
            scene.Items.Insert(target, item);
            Renumber(scene);
            if (scene.IsLive)
            {
                Emit("SceneItemListReindexed", Obj(
                    ("sceneItems", ItemListJson(scene, basic: true)),
                    ("sceneName", scene.Name),
                    ("sceneUuid", scene.UuidText)));
            }
        }

        /// <summary>Mutes or unmutes an input (SetInputMute). Nothing is announced when it does not change.</summary>
        public void SetMuted(SimInput input, bool muted)
        {
            EnsureAudio(input);
            if (input.Muted == muted)
            {
                return;
            }

            input.Muted = muted;
            Emit("InputMuteStateChanged", Obj(("inputMuted", muted), ("inputName", input.Name), ("inputUuid", input.UuidText)));
        }

        /// <summary>Changes the mute state of an input (ToggleInputMute) and returns the new state.</summary>
        public bool ToggleMuted(SimInput input)
        {
            EnsureAudio(input);
            SetMuted(input, !input.Muted);
            return input.Muted;
        }

        /// <summary>Sets the volume of an input (SetInputVolume).</summary>
        public void SetVolume(SimInput input, float mul)
        {
            EnsureAudio(input);

            // ASSUMPTION: the brief asks for an event when the volume changes. The libobs source signals every call of obs_source_set_volume,
            // also one that sets the same volume; a measurement of SetInputVolume with the current volume would tell.
            if (input.Volume == mul)
            {
                return;
            }

            input.Volume = mul;
            Emit("InputVolumeChanged", Obj(
                ("inputName", input.Name),
                ("inputUuid", input.UuidText),
                ("inputVolumeDb", MulToDb(mul)),
                ("inputVolumeMul", (double)mul)));
        }

        /// <summary>
        /// Changes the settings of an input (SetInputSettings): the given fields are laid over the settings that it has, or the settings are taken back to nothing first (the defaults come from the
        /// kind) and the given fields are all it has. The event says all the settings, and comes for every call, also when nothing changed: libobs signals every call of obs_source_update (two equal calls
        /// made one event each, measured with OBS Studio 33.0.0; a source with video applies an update in its next tick, so calls in one tick make one event).
        /// </summary>
        public void SetSettings(SimInput input, JsonObject given, bool overlay)
        {
            // Measured with OBS Studio 33.0.0: a field that is an object replaces the object that was there, as a whole (the other fields of that object are gone), and a field that is null is ignored (the
            // field keeps what it was). obs-websocket makes the settings of the request with JsonToObsData, which skips a null at any depth.
            var merged = overlay ? (JsonObject)input.Settings.DeepClone() : new JsonObject();
            foreach (var (name, value) in given)
            {
                if (WithoutNulls(value?.DeepClone()) is { } kept)
                {
                    merged[name] = kept;
                }
            }

            input.Settings = (JsonObject)Sorted(merged)!;
            Emit("InputSettingsChanged", Obj(("inputName", input.Name), ("inputSettings", input.Settings.DeepClone()), ("inputUuid", input.UuidText)));
        }

        // The node without its fields that are null, at any depth; null for a node that is null.
        private static JsonNode? WithoutNulls(JsonNode? node)
        {
            switch (node)
            {
                case JsonObject fields:
                    foreach (var name in fields.Where(field => field.Value is null).Select(field => field.Key).ToArray())
                    {
                        fields.Remove(name);
                    }

                    foreach (var field in fields)
                    {
                        WithoutNulls(field.Value);
                    }

                    return fields;
                case JsonArray items:
                    foreach (var item in items)
                    {
                        WithoutNulls(item);
                    }

                    return items;
                default:
                    return node;
            }
        }

        /// <summary>Sets the balance of the audio of an input (SetInputAudioBalance). obs_source_set_balance_value signals every call.</summary>
        public void SetBalance(SimInput input, float balance)
        {
            EnsureAudio(input);
            input.Balance = balance;
            Emit("InputAudioBalanceChanged", Obj(("inputAudioBalance", (double)balance), ("inputName", input.Name), ("inputUuid", input.UuidText)));
        }

        /// <summary>Sets the sync offset of the audio of an input, in milliseconds (SetInputAudioSyncOffset). obs_source_set_sync_offset signals every call.</summary>
        public void SetSyncOffset(SimInput input, int milliseconds)
        {
            EnsureAudio(input);
            input.SyncOffset = milliseconds;
            Emit("InputAudioSyncOffsetChanged", Obj(("inputAudioSyncOffset", milliseconds), ("inputName", input.Name), ("inputUuid", input.UuidText)));
        }

        /// <summary>
        /// Sets the monitor type of an input (SetInputAudioMonitorType). OBS Studio 33.0.0 has a switch that is on or off, and turns every type but none into monitor and output (measured: the input held
        /// OBS_MONITORING_TYPE_MONITOR_AND_OUTPUT after it was told OBS_MONITORING_TYPE_MONITOR_ONLY: libobs keeps obs_source_set_monitoring_type for compatibility, and it only says whether monitoring is on).
        /// Nothing is announced when the type that is held does not change (measured: two equal requests, one event).
        /// </summary>
        public void SetMonitorType(SimInput input, string monitorType)
        {
            EnsureAudio(input);
            var held = monitorType == "OBS_MONITORING_TYPE_NONE" ? monitorType : "OBS_MONITORING_TYPE_MONITOR_AND_OUTPUT";
            if (input.MonitorType == held)
            {
                return;
            }

            input.MonitorType = held;
            Emit("InputAudioMonitorTypeChanged", Obj(("inputName", input.Name), ("inputUuid", input.UuidText), ("monitorType", held)));
        }

        /// <summary>Sets the audio tracks of an input, one bit for each (SetInputAudioTracks). Nothing is announced when they do not change (measured with OBS Studio 33.0.0: two equal requests, one event).</summary>
        public void SetTracks(SimInput input, int tracks)
        {
            EnsureAudio(input);
            if (input.Tracks == tracks)
            {
                return;
            }

            input.Tracks = tracks;
            Emit("InputAudioTracksChanged", Obj(("inputAudioTracks", TracksJson(tracks)), ("inputName", input.Name), ("inputUuid", input.UuidText)));
        }

        /// <summary>Writes the audio tracks as obs-websocket does: an object with the numbers 1 to 6 as the names of the fields and a boolean for each.</summary>
        public static JsonObject TracksJson(int tracks)
        {
            var json = new JsonObject();
            for (var track = 1; track <= 6; track++)
            {
                json[track.ToString(System.Globalization.CultureInfo.InvariantCulture)] = (tracks & (1 << (track - 1))) != 0;
            }

            return json;
        }

        /// <summary>How long the media of every media input is, in milliseconds.</summary>
        public const int MediaDuration = 60000;

        /// <summary>
        /// Tells the media of an input to play, pause, stop or restart (TriggerMediaInputAction), as OBS Studio 33.0.0 does with a media source (measured: LiveMediaTests). A play, also of a media that plays
        /// already, announces that the playback started and then that play was triggered; a pause, a stop and a restart announce that they were triggered, and a restart says nothing of a start. A pause of a
        /// media that does not play opens it and holds it at the beginning. Next and previous do nothing for a media source (it has no playlist), and say nothing. An input that is no media input
        /// answers all of them with success and does nothing (measured with a color source).
        /// </summary>
        public void TriggerMedia(SimInput input, string action)
        {
            if (!input.Kind.IsMedia)
            {
                return;
            }

            var opened = input.MediaState is "OBS_MEDIA_STATE_PLAYING" or "OBS_MEDIA_STATE_PAUSED";
            switch (action)
            {
                case "OBS_WEBSOCKET_MEDIA_INPUT_ACTION_PLAY":
                    input.MediaCursor = opened ? input.MediaCursor : 0;
                    input.MediaState = "OBS_MEDIA_STATE_PLAYING";
                    Emit("MediaInputPlaybackStarted", Obj(("inputName", input.Name), ("inputUuid", input.UuidText)));
                    EmitMediaAction(input, action);
                    break;
                case "OBS_WEBSOCKET_MEDIA_INPUT_ACTION_PAUSE":
                    input.MediaCursor = opened ? input.MediaCursor : 0;
                    input.MediaState = "OBS_MEDIA_STATE_PAUSED";
                    EmitMediaAction(input, action);
                    break;
                case "OBS_WEBSOCKET_MEDIA_INPUT_ACTION_STOP":
                    input.MediaCursor = 0;
                    input.MediaState = "OBS_MEDIA_STATE_STOPPED";
                    EmitMediaAction(input, action);
                    break;
                case "OBS_WEBSOCKET_MEDIA_INPUT_ACTION_RESTART":
                    input.MediaCursor = 0;
                    input.MediaState = "OBS_MEDIA_STATE_PLAYING";
                    EmitMediaAction(input, action);
                    break;
            }
        }

        /// <summary>The media of an input played to its end (or could not be opened), which OBS Studio announces. It happens by itself in OBS Studio; here someone else makes it happen.</summary>
        public void EndMedia(SimInput input)
        {
            if (!input.Kind.IsMedia || input.MediaState == "OBS_MEDIA_STATE_ENDED")
            {
                return;
            }

            input.MediaCursor = 0;
            input.MediaState = "OBS_MEDIA_STATE_ENDED";
            Emit("MediaInputPlaybackEnded", Obj(("inputName", input.Name), ("inputUuid", input.UuidText)));
        }

        /// <summary>
        /// Moves the media of an input to a position, or by an offset (SetMediaInputCursor, OffsetMediaInputCursor): only while it plays or is paused. obs-websocket does not check the position against the
        /// length, and an offset does not go before the beginning. Nothing is announced.
        /// </summary>
        public void MoveMedia(SimInput input, long amount, bool relative)
        {
            if (input.MediaState is not ("OBS_MEDIA_STATE_PLAYING" or "OBS_MEDIA_STATE_PAUSED"))
            {
                throw new SimFailure(SimStatus.InvalidResourceState, "The media input must be playing or paused in order to set the cursor position.");
            }

            var target = relative ? input.MediaCursor + amount : amount;
            input.MediaCursor = (int)Math.Clamp(target, 0, int.MaxValue);
        }

        private void EmitMediaAction(SimInput input, string action)
        {
            Emit("MediaInputActionTriggered", Obj(("inputName", input.Name), ("inputUuid", input.UuidText), ("mediaAction", action)));
        }

        /// <summary>Switches the program scene (SetCurrentProgramScene): at once for everything that reads it, and with a transition for the events (fact 25).</summary>
        public void SwitchProgram(SimScene scene)
        {
            if (scene.Removed)
            {
                // A removed scene has no canvas any more, and obs-websocket only switches to a scene of the main canvas.
                // ASSUMPTION: read from the source of obs-websocket 5.7.6, not measured. SetCurrentProgramScene for a scene that was removed
                // and is alive, in a real OBS Studio, would tell.
                throw new SimFailure(SimStatus.InvalidResourceState, "The specified scene is not from the main canvas and cannot be set as the program scene.");
            }

            if (ReferenceEquals(scene, _program))
            {
                // ASSUMPTION: switching to the scene that is on air already changes nothing and starts no transition. Not measured.
                return;
            }

            _program = scene;
            StartTransition();
        }

        /// <summary>Checks that an input is free to be given a name: no input that is alive, and no scene of the list, has it.</summary>
        public void EnsureInputNameFree(string name, string comment)
        {
            if (FindInput(name) is not null || FindLiveScene(name) is not null)
            {
                throw SimFailure.Exists(comment);
            }
        }

        /// <summary>Converts a volume in dB to a multiplier, as libobs does, in single precision.</summary>
        public static float DbToMul(double db)
        {
            var value = (float)db;
            return float.IsFinite(value) ? MathF.Pow(10f, value / 20f) : 0f;
        }

        /// <summary>Converts a multiplier to dB, as obs-websocket reports it: single precision, and -100 for a multiplier of zero.</summary>
        public static double MulToDb(float mul)
        {
            if (mul == 0f)
            {
                return -100.0;
            }

            return 20f * MathF.Log10(MathF.Abs(mul));
        }

        private static void Renumber(SimScene scene)
        {
            for (var index = 0; index < scene.Items.Count; index++)
            {
                scene.Items[index].Index = index;
            }
        }

        // Whether a scene holds the target, in itself or in a scene that it holds, at any depth. A scene that is held several times is visited once.
        private static bool Reaches(SimScene from, SimScene target)
        {
            var visited = new HashSet<SimScene>();
            var pending = new Stack<SimScene>();
            pending.Push(from);
            while (pending.Count > 0)
            {
                var scene = pending.Pop();
                if (ReferenceEquals(scene, target))
                {
                    return true;
                }

                if (!visited.Add(scene))
                {
                    continue;
                }

                foreach (var item in scene.Items)
                {
                    if (item.Source is SimScene nested)
                    {
                        pending.Push(nested);
                    }
                }
            }

            return false;
        }

        /// <summary>Checks that an input has audio: only then does it have a mute state and a volume (fact 18).</summary>
        public static void EnsureAudio(SimInput input)
        {
            if (!input.Kind.HasAudio)
            {
                throw new SimFailure(SimStatus.InvalidResourceState, "The specified input does not support audio.");
            }
        }

        private long NextSerial()
        {
            return ++_serial;
        }

        private SimInput NewInput(string name, SimInputKind kind, JsonObject? settings)
        {
            var input = new SimInput(_obs.Random.NextGuid(), name, NextSerial(), kind, (JsonObject)Sorted(settings ?? new JsonObject())!);
            _byUuid[input.UuidText] = input;
            _inputs.Add(input);
            return input;
        }

        private SimItem AddItemCore(SimScene scene, SimSource source, bool enabled)
        {
            var item = new SimItem(scene.NextItemId++, scene, source) { Index = scene.Items.Count };
            scene.Items.Add(item);
            source.References++;
            if (scene.IsLive)
            {
                Emit("SceneItemCreated", Obj(
                    ("sceneItemId", item.Id),
                    ("sceneItemIndex", item.Index),
                    ("sceneName", scene.Name),
                    ("sceneUuid", scene.UuidText),
                    ("sourceName", source.Name),
                    ("sourceUuid", source.UuidText)));
            }

            if (!enabled)
            {
                // ASSUMPTION: an item that is created disabled is created enabled, and then disabled (ActionHelper::CreateSceneItem), which
                // announces the change right after the creation. Not measured.
                item.Enabled = false;
                if (scene.IsLive)
                {
                    Emit("SceneItemEnableStateChanged", Obj(
                        ("sceneItemEnabled", false),
                        ("sceneItemId", item.Id),
                        ("sceneName", scene.Name),
                        ("sceneUuid", scene.UuidText)));
                }
            }

            if (scene.IsLive)
            {
                // ASSUMPTION: the user interface selects every item that is created, whatever the source (the brief names the items of inputs).
                Emit("SceneItemSelected", Obj(("sceneItemId", item.Id), ("sceneName", scene.Name), ("sceneUuid", scene.UuidText)));
            }

            return item;
        }

        // A reference to a source is let go of. When it was the last, and the source is an input without a pin or a removed scene, it is
        // destroyed, which lets go of what it refers to in the same way. A destroyed scene announces nothing about its items (fact 7).
        private void ReleaseSource(SimSource source, int anchor, TimeSpan gap)
        {
            source.References--;
            if (source.References > 0)
            {
                return;
            }

            if (source is SimInput { Pins: 0 } input)
            {
                DestroyInput(input, anchor, gap);
            }
            else if (source is SimScene { Removed: true, Destroyed: false } scene)
            {
                DestroyScene(scene, anchor, gap);
            }
        }

        private void DestroyInput(SimInput input, int anchor, TimeSpan gap)
        {
            input.Destroyed = true;
            _byUuid.Remove(input.UuidText);
            _inputs.Remove(input);

            // An input is announced once: when it is removed or, when it never was, when it is destroyed (fact 6).
            if (!input.Removed)
            {
                input.Removed = true;
                Emit("InputRemoved", InputRemovedData(input), anchor, gap);
            }
        }

        private void DestroyScene(SimScene scene, int anchor, TimeSpan gap)
        {
            scene.Destroyed = true;
            _byUuid.Remove(scene.UuidText);
            _detached.Remove(scene);
            var items = scene.Items.ToArray();
            scene.Items.Clear();
            foreach (var item in items)
            {
                ReleaseSource(item.Source, anchor, gap);
            }
        }

        private int Emit(string type, JsonObject data, int anchor = -1, TimeSpan gap = default)
        {
            if (type == "InputRemoved" && !_announcedInputs.Add((string)data["inputUuid"]!))
            {
                _violations.Add($"InputRemoved was emitted twice for the input {data["inputUuid"]}.");
            }

            if (type == "SceneRemoved" && !_announcedScenes.Add((string)data["sceneUuid"]!))
            {
                _violations.Add($"SceneRemoved was emitted twice for the scene {data["sceneUuid"]}.");
            }

            return _obs.Emit(type, data, anchor, gap);
        }

        private JsonObject ProgramSceneData()
        {
            return Obj(("sceneName", _program.Name), ("sceneUuid", _program.UuidText));
        }

        private static JsonObject SceneCreatedData(SimScene scene)
        {
            return Obj(("isGroup", false), ("sceneName", scene.Name), ("sceneUuid", scene.UuidText));
        }

        private static JsonObject SceneRemovedData(SimScene scene)
        {
            return Obj(("isGroup", false), ("sceneName", scene.Name), ("sceneUuid", scene.UuidText));
        }

        private static JsonObject InputRemovedData(SimInput input)
        {
            return Obj(("inputName", input.Name), ("inputUuid", input.UuidText));
        }

        private static JsonObject InputCreatedData(SimInput input)
        {
            return Obj(
                ("defaultInputSettings", Sorted(input.Kind.MakeDefaultSettings())),
                ("inputKind", input.Kind.Id),
                ("inputKindCaps", input.Kind.Caps),
                ("inputName", input.Name),
                ("inputSettings", input.Settings.DeepClone()),
                ("inputUuid", input.UuidText),
                ("unversionedInputKind", input.Kind.UnversionedId));
        }

        private void StartTransition()
        {
            CancelTransition();

            var duration = _obs.Quirks.TransitionDuration;
            var transition = duration > TimeSpan.Zero
                ? new Transition(FadeName, FadeUuid)
                : new Transition(CutName, CutUuid);
            _transition = transition;
            Emit("SceneTransitionStarted", TransitionData(transition));

            var total = duration + _obs.Quirks.TransitionOverhead.Sample(_obs.Random);
            if (total <= TimeSpan.Zero)
            {
                CompleteTransition(transition);
            }
            else
            {
                _obs.Schedule(Now + total, () => CompleteTransition(transition));
            }
        }

        // A transition that is running ends when another switch starts (fact 25): its end is announced at that moment, and the scene that it
        // was going to is not announced as the program scene.
        private void CancelTransition()
        {
            if (_transition is not { } running)
            {
                return;
            }

            running.Cancelled = true;
            _transition = null;
            Emit("SceneTransitionEnded", TransitionData(running));
        }

        private void CompleteTransition(Transition transition)
        {
            if (transition.Cancelled || !ReferenceEquals(_transition, transition))
            {
                return;
            }

            _transition = null;
            Emit("SceneTransitionVideoEnded", TransitionData(transition));
            Emit("CurrentProgramSceneChanged", ProgramSceneData());
            Emit("SceneTransitionEnded", TransitionData(transition));
        }

        private static JsonObject TransitionData(Transition transition)
        {
            return Obj(("transitionName", transition.Name), ("transitionUuid", transition.Uuid));
        }

        private sealed class Transition
        {
            public Transition(string name, string uuid)
            {
                Name = name;
                Uuid = uuid;
            }

            public string Name { get; }

            public string Uuid { get; }

            public bool Cancelled { get; set; }
        }
    }
}
