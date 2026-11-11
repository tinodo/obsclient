namespace OBSStudioClient.Tests.Simulation
{
    using System.Globalization;
    using System.Text.Json;

    /// <summary>
    /// Rebuilds the state of a simulated OBS Studio from the events that it sent, and nothing else, the way a model of OBS Studio does,
    /// and compares it with the state that OBS Studio really has. It tells whether the events are enough to follow OBS Studio. It
    /// can only be exact when the events arrive in the order in which they were emitted, so it is meant for <see cref="SimQuirks.Ideal"/>.
    /// </summary>
    internal static class SimReplay
    {
        /// <summary>Replays the events, and lists what is wrong with them, and how the result differs from the final snapshot.</summary>
        /// <param name="initial">The snapshot from before the first event.</param>
        /// <param name="messages">The messages that were sent, in the order in which they were sent. Only the events are used.</param>
        /// <param name="final">The snapshot from after the last event.</param>
        public static List<string> Compare(SimSnapshot initial, IEnumerable<SimMessage> messages, SimSnapshot final)
        {
            var problems = new List<string>();
            var scenes = new List<Scene>();
            var inputs = new Dictionary<Guid, Input>();
            foreach (var scene in initial.Scenes)
            {
                var replayed = new Scene(scene.Uuid, scene.Name);
                foreach (var item in scene.Items.OrderBy(item => item.Index))
                {
                    replayed.Items.Add(new Item(item.Id, item.SourceUuid) { Enabled = item.Enabled, Locked = item.Locked });
                }

                scenes.Add(replayed);
            }

            foreach (var input in initial.Inputs)
            {
                inputs[input.Uuid] = new Input(input.Uuid, input.Name, input.Kind) { Muted = input.Muted, Mul = input.VolumeMul, Removed = input.Removed, Settings = input.Settings, Balance = input.Balance, SyncOffset = input.SyncOffset, Monitor = input.MonitorType, Tracks = input.Tracks, Media = input.MediaState };
            }

            var program = initial.ProgramSceneUuid;
            var number = 0;
            foreach (var message in messages)
            {
                if (message.Op != SimOp.Event)
                {
                    continue;
                }

                number++;
                using var document = JsonDocument.Parse(message.Json);
                var type = document.RootElement.GetProperty("d").GetProperty("eventType").GetString()!;
                var data = document.RootElement.GetProperty("d").GetProperty("eventData");
                var at = $"event {number.ToString(CultureInfo.InvariantCulture)} ({type})";
                switch (type)
                {
                    case "SceneCreated":
                        var created = Guid.Parse(data.GetProperty("sceneUuid").GetString()!);
                        if (scenes.Any(scene => scene.Uuid == created))
                        {
                            problems.Add($"{at}: the scene {created} was announced as created, and it is there already.");
                            break;
                        }

                        scenes.Insert(0, new Scene(created, data.GetProperty("sceneName").GetString()!));
                        break;
                    case "SceneRemoved":
                        var removed = Guid.Parse(data.GetProperty("sceneUuid").GetString()!);
                        if (scenes.RemoveAll(scene => scene.Uuid == removed) != 1)
                        {
                            problems.Add($"{at}: the scene {removed} was announced as removed, and it was not there.");
                        }

                        break;
                    case "SceneListChanged":
                        var listed = data.GetProperty("scenes").EnumerateArray().Select(scene => $"{scene.GetProperty("sceneIndex").GetInt32()} {scene.GetProperty("sceneUuid").GetString()}").ToArray();
                        var expected = scenes.Select((scene, index) => $"{index} {scene.Uuid:D}").ToArray();
                        if (!listed.SequenceEqual(expected))
                        {
                            problems.Add($"{at}: lists [{string.Join(", ", listed)}] and the scenes are [{string.Join(", ", expected)}].");
                        }

                        break;
                    case "SceneNameChanged":
                        var renamed = Find(scenes, data.GetProperty("sceneUuid").GetString()!, problems, at);
                        if (renamed is not null)
                        {
                            if (renamed.Name != data.GetProperty("oldSceneName").GetString())
                            {
                                problems.Add($"{at}: the old name is \"{data.GetProperty("oldSceneName").GetString()}\" and the scene is named \"{renamed.Name}\".");
                            }

                            renamed.Name = data.GetProperty("sceneName").GetString()!;
                        }

                        break;
                    case "CurrentProgramSceneChanged":
                        program = Guid.Parse(data.GetProperty("sceneUuid").GetString()!);
                        break;
                    case "InputCreated":
                        var createdInput = Guid.Parse(data.GetProperty("inputUuid").GetString()!);
                        if (!inputs.TryAdd(createdInput, new Input(createdInput, data.GetProperty("inputName").GetString()!, data.GetProperty("inputKind").GetString()!) { Settings = Canonical(data.GetProperty("inputSettings")) }))
                        {
                            problems.Add($"{at}: the input {createdInput} was announced as created, and it is there already.");
                        }

                        break;
                    case "InputRemoved":
                        // An input that was removed stays alive while a scene item holds it, and goes on announcing its mute state and volume.
                        if (inputs.TryGetValue(Guid.Parse(data.GetProperty("inputUuid").GetString()!), out var gone))
                        {
                            if (gone.Removed)
                            {
                                problems.Add($"{at}: the input {data.GetProperty("inputUuid").GetString()} was announced as removed twice.");
                            }

                            gone.Removed = true;
                        }
                        else
                        {
                            problems.Add($"{at}: the input {data.GetProperty("inputUuid").GetString()} was announced as removed, and it was not there.");
                        }

                        break;
                    case "InputNameChanged":
                        if (inputs.TryGetValue(Guid.Parse(data.GetProperty("inputUuid").GetString()!), out var renamedInput))
                        {
                            if (renamedInput.Name != data.GetProperty("oldInputName").GetString())
                            {
                                problems.Add($"{at}: the old name is \"{data.GetProperty("oldInputName").GetString()}\" and the input is named \"{renamedInput.Name}\".");
                            }

                            renamedInput.Name = data.GetProperty("inputName").GetString()!;
                        }
                        else
                        {
                            problems.Add($"{at}: the input {data.GetProperty("inputUuid").GetString()} was renamed, and it is not there.");
                        }

                        break;
                    case "InputMuteStateChanged":
                        if (inputs.TryGetValue(Guid.Parse(data.GetProperty("inputUuid").GetString()!), out var muted))
                        {
                            muted.Muted = data.GetProperty("inputMuted").GetBoolean();
                        }
                        else
                        {
                            problems.Add($"{at}: the mute state of the input {data.GetProperty("inputUuid").GetString()} changed, and it is not there.");
                        }

                        break;
                    case "InputVolumeChanged":
                        if (inputs.TryGetValue(Guid.Parse(data.GetProperty("inputUuid").GetString()!), out var volume))
                        {
                            volume.Mul = data.GetProperty("inputVolumeMul").GetDouble();
                        }
                        else
                        {
                            problems.Add($"{at}: the volume of the input {data.GetProperty("inputUuid").GetString()} changed, and it is not there.");
                        }

                        break;
                    case "InputSettingsChanged":
                    case "InputAudioBalanceChanged":
                    case "InputAudioSyncOffsetChanged":
                    case "InputAudioMonitorTypeChanged":
                    case "InputAudioTracksChanged":
                    case "MediaInputPlaybackStarted":
                    case "MediaInputPlaybackEnded":
                    case "MediaInputActionTriggered":
                        if (inputs.TryGetValue(Guid.Parse(data.GetProperty("inputUuid").GetString()!), out var changedInput))
                        {
                            Apply(changedInput, type, data);
                        }
                        else
                        {
                            problems.Add($"{at}: the input {data.GetProperty("inputUuid").GetString()} was changed ({type}), and it is not there.");
                        }

                        break;
                    case "SceneItemCreated":
                        var addTo = Find(scenes, data.GetProperty("sceneUuid").GetString()!, problems, at);
                        if (addTo is not null)
                        {
                            var index = data.GetProperty("sceneItemIndex").GetInt32();
                            if (index != addTo.Items.Count)
                            {
                                problems.Add($"{at}: the item is created at index {index} and the scene has {addTo.Items.Count} items.");
                            }

                            addTo.Items.Add(new Item(data.GetProperty("sceneItemId").GetInt32(), Guid.Parse(data.GetProperty("sourceUuid").GetString()!)));
                        }

                        break;
                    case "SceneItemRemoved":
                        var removeFrom = Find(scenes, data.GetProperty("sceneUuid").GetString()!, problems, at);
                        if (removeFrom is not null && removeFrom.Items.RemoveAll(item => item.Id == data.GetProperty("sceneItemId").GetInt32()) != 1)
                        {
                            problems.Add($"{at}: the item {data.GetProperty("sceneItemId").GetInt32()} was removed, and it was not in the scene.");
                        }

                        break;
                    case "SceneItemListReindexed":
                        var reorder = Find(scenes, data.GetProperty("sceneUuid").GetString()!, problems, at);
                        if (reorder is not null)
                        {
                            var order = data.GetProperty("sceneItems").EnumerateArray()
                                .Select(item => (Id: item.GetProperty("sceneItemId").GetInt32(), Index: item.GetProperty("sceneItemIndex").GetInt32()))
                                .OrderBy(item => item.Index)
                                .ToArray();
                            if (!order.Select(item => item.Index).SequenceEqual(Enumerable.Range(0, reorder.Items.Count))
                                || !order.Select(item => item.Id).Order().SequenceEqual(reorder.Items.Select(item => item.Id).Order()))
                            {
                                problems.Add($"{at}: does not list exactly the items of the scene, with the indexes 0 to {reorder.Items.Count - 1}.");
                                break;
                            }

                            reorder.Items = order.Select(entry => reorder.Items.First(item => item.Id == entry.Id)).ToList();
                        }

                        break;
                    case "SceneItemEnableStateChanged":
                        var enableIn = Find(scenes, data.GetProperty("sceneUuid").GetString()!, problems, at);
                        var enabled = enableIn?.Items.FirstOrDefault(item => item.Id == data.GetProperty("sceneItemId").GetInt32());
                        if (enabled is null)
                        {
                            problems.Add($"{at}: the item is not in the scene.");
                        }
                        else
                        {
                            enabled.Enabled = data.GetProperty("sceneItemEnabled").GetBoolean();
                        }

                        break;
                    case "SceneItemLockStateChanged":
                        var lockIn = Find(scenes, data.GetProperty("sceneUuid").GetString()!, problems, at);
                        var locked = lockIn?.Items.FirstOrDefault(item => item.Id == data.GetProperty("sceneItemId").GetInt32());
                        if (locked is null)
                        {
                            problems.Add($"{at}: the item is not in the scene.");
                        }
                        else
                        {
                            locked.Locked = data.GetProperty("sceneItemLocked").GetBoolean();
                        }

                        break;
                }
            }

            Same(problems, "scenes", scenes.Select(scene => $"{scene.Uuid:D} \"{scene.Name}\""), final.Scenes.Select(scene => $"{scene.Uuid:D} \"{scene.Name}\""));
            if (program != final.ProgramSceneUuid)
            {
                problems.Add($"The events say that the program scene is {program}, and it is {final.ProgramSceneUuid}.");
            }

            foreach (var scene in final.Scenes)
            {
                var replayed = scenes.FirstOrDefault(candidate => candidate.Uuid == scene.Uuid);
                if (replayed is not null)
                {
                    Same(
                        problems,
                        $"items of \"{scene.Name}\"",
                        replayed.Items.Select((item, index) => $"{item.Id} at {index} {item.Source:D} {(item.Enabled ? "enabled" : "disabled")} {(item.Locked ? "locked" : "unlocked")}"),
                        scene.Items.Select(item => $"{item.Id} at {item.Index} {item.SourceUuid:D} {(item.Enabled ? "enabled" : "disabled")} {(item.Locked ? "locked" : "unlocked")}"));
                }
            }

            // An input that was removed and is still alive is not one that the events leave in the list.
            Same(
                problems,
                "inputs",
                inputs.Values.Where(input => !input.Removed).OrderBy(input => input.Uuid).Select(Describe),
                final.Inputs.Where(input => !input.Removed).OrderBy(input => input.Uuid).Select(Describe));

            // OBS goes on announcing the mute state and the volume of a removed input that it keeps alive, and what the events say of it has to be right too.
            // No event tells that OBS destroyed a removed input, so which ones are compared is what the final state still holds.
            var retained = final.Inputs.Where(input => input.Removed).OrderBy(input => input.Uuid).ToArray();
            Same(
                problems,
                "removed inputs that OBS Studio still holds",
                retained.Select(input => inputs.TryGetValue(input.Uuid, out var replayedInput) && replayedInput.Removed
                    ? Describe(replayedInput)
                    : $"{input.Uuid:D} (not announced as removed)"),
                retained.Select(Describe));
            return problems;
        }

        // What an event of an input says: the settings (all of them), one of the details of its audio, or what its media does.
        private static void Apply(Input input, string type, JsonElement data)
        {
            switch (type)
            {
                case "InputSettingsChanged":
                    input.Settings = Canonical(data.GetProperty("inputSettings"));
                    break;
                case "InputAudioBalanceChanged":
                    input.Balance = (float)data.GetProperty("inputAudioBalance").GetDouble();
                    break;
                case "InputAudioSyncOffsetChanged":
                    input.SyncOffset = data.GetProperty("inputAudioSyncOffset").GetInt32();
                    break;
                case "InputAudioMonitorTypeChanged":
                    input.Monitor = data.GetProperty("monitorType").GetString()!;
                    break;
                case "MediaInputPlaybackStarted":
                    input.Media = "OBS_MEDIA_STATE_PLAYING";
                    break;
                case "MediaInputPlaybackEnded":
                    input.Media = "OBS_MEDIA_STATE_ENDED";
                    break;
                case "MediaInputActionTriggered":
                    // The way that the model reads these events (next and previous say nothing about the state; the model reads it then).
                    input.Media = data.GetProperty("mediaAction").GetString() switch
                    {
                        "OBS_WEBSOCKET_MEDIA_INPUT_ACTION_PLAY" or "OBS_WEBSOCKET_MEDIA_INPUT_ACTION_RESTART" => "OBS_MEDIA_STATE_PLAYING",
                        "OBS_WEBSOCKET_MEDIA_INPUT_ACTION_PAUSE" => "OBS_MEDIA_STATE_PAUSED",
                        "OBS_WEBSOCKET_MEDIA_INPUT_ACTION_STOP" => "OBS_MEDIA_STATE_STOPPED",
                        _ => input.Media,
                    };
                    break;
                default:
                    var tracks = data.GetProperty("inputAudioTracks");
                    input.Tracks = Enumerable.Range(1, 6).Sum(track => tracks.GetProperty(track.ToString(CultureInfo.InvariantCulture)).GetBoolean() ? 1 << (track - 1) : 0);
                    break;
            }
        }

        private static string Canonical(JsonElement settings)
        {
            return SimJson.Serialize(System.Text.Json.Nodes.JsonNode.Parse(settings.GetRawText())!);
        }

        private static string Describe(Input input)
        {
            return Describe(input.Uuid, input.Name, input.Kind, input.Muted, input.Mul, input.Balance, input.SyncOffset, input.Monitor, input.Tracks, input.Settings, input.Media);
        }

        private static string Describe(SimInputSnapshot input)
        {
            return Describe(input.Uuid, input.Name, input.Kind, input.Muted, input.VolumeMul, input.Balance, input.SyncOffset, input.MonitorType, input.Tracks, input.Settings, input.MediaState);
        }

        private static string Describe(Guid uuid, string name, string kind, bool muted, double mul, float balance, int offset, string monitor, int tracks, string settings, string? media)
        {
            var audio = SimInputKinds.Find(kind)!.HasAudio
                ? $" {(muted ? "muted" : "unmuted")} mul={mul.ToString("R", CultureInfo.InvariantCulture)} balance={balance.ToString("R", CultureInfo.InvariantCulture)} offset={offset.ToString(CultureInfo.InvariantCulture)} monitor={monitor} tracks={tracks.ToString(CultureInfo.InvariantCulture)}"
                : string.Empty;
            return $"{uuid:D} \"{name}\" {kind}{audio}{(media is null ? string.Empty : " media=" + media)} settings={settings}";
        }

        private static Scene? Find(List<Scene> scenes, string uuid, List<string> problems, string at)
        {
            var scene = scenes.FirstOrDefault(candidate => candidate.Uuid == Guid.Parse(uuid));
            if (scene is null)
            {
                problems.Add($"{at}: is about the scene {uuid}, which is not in the list of scenes.");
            }

            return scene;
        }

        private static void Same(List<string> problems, string what, IEnumerable<string> replayed, IEnumerable<string> really)
        {
            var left = replayed.ToArray();
            var right = really.ToArray();
            if (!left.SequenceEqual(right))
            {
                problems.Add($"The {what} that the events leave: [{string.Join("; ", left)}], and what OBS Studio has: [{string.Join("; ", right)}].");
            }
        }

        private sealed class Scene(Guid uuid, string name)
        {
            public Guid Uuid { get; } = uuid;

            public string Name { get; set; } = name;

            public List<Item> Items { get; set; } = [];
        }

        private sealed class Item(int id, Guid source)
        {
            public int Id { get; } = id;

            public Guid Source { get; } = source;

            public bool Enabled { get; set; } = true;

            public bool Locked { get; set; }
        }

        private sealed class Input(Guid uuid, string name, string kind)
        {
            public Guid Uuid { get; } = uuid;

            public string Name { get; set; } = name;

            public string Kind { get; } = kind;

            public bool Muted { get; set; }

            public bool Removed { get; set; }

            public double Mul { get; set; } = 1.0;

            public string Settings { get; set; } = "{}";

            public float Balance { get; set; } = 0.5f;

            public int SyncOffset { get; set; }

            public string Monitor { get; set; } = "OBS_MONITORING_TYPE_NONE";

            public int Tracks { get; set; } = 63;

            // What the media of the input does; null for an input that plays no media. A new media input has not been opened.
            public string? Media { get; set; } = SimInputKinds.Find(kind)!.IsMedia ? "OBS_MEDIA_STATE_NONE" : null;
        }
    }
}
