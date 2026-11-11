namespace OBSStudioClient.Tests.Simulation
{
    using System.Text.Json.Nodes;
    using static OBSStudioClient.Tests.Simulation.SimJson;

    /// <summary>An input as OBS Studio saves it in the file of a scene collection.</summary>
    /// <param name="Uuid">The UUID of the input. It is saved, so it is the same when the collection is loaded again.</param>
    /// <param name="Name">The name of the input.</param>
    /// <param name="Kind">The kind of the input.</param>
    /// <param name="Settings">The settings of the input.</param>
    /// <param name="Muted">Whether the input is muted.</param>
    /// <param name="Volume">The volume of the input, as a multiplier.</param>
    /// <param name="Pins">The number of pins of the input: a global audio device is kept by the collection.</param>
    /// <param name="Balance">The balance of the audio.</param>
    /// <param name="SyncOffset">The sync offset of the audio, in milliseconds.</param>
    /// <param name="MonitorType">The monitor type.</param>
    /// <param name="Tracks">The audio tracks, one bit for each.</param>
    internal sealed record SimSavedInput(Guid Uuid, string Name, SimInputKind Kind, JsonObject Settings, bool Muted, float Volume, int Pins, float Balance, int SyncOffset, string MonitorType, int Tracks);

    /// <summary>A scene item as OBS Studio saves it. Its place in the list of the items of the scene is its index.</summary>
    /// <param name="Id">The ID of the item.</param>
    /// <param name="SourceUuid">The UUID of the scene or the input that the item shows.</param>
    /// <param name="Enabled">Whether the item is enabled.</param>
    /// <param name="Locked">Whether the item is locked.</param>
    /// <param name="Transform">The transform of the item, its own copy.</param>
    /// <param name="BlendMode">The blend mode of the item.</param>
    internal sealed record SimSavedItem(int Id, Guid SourceUuid, bool Enabled, bool Locked, SimTransform Transform, string BlendMode);

    /// <summary>A scene as OBS Studio saves it, with its items from the bottom.</summary>
    /// <param name="Uuid">The UUID of the scene.</param>
    /// <param name="Name">The name of the scene.</param>
    /// <param name="NextItemId">The ID that the next item of the scene gets.</param>
    /// <param name="Items">The items of the scene, in the order of their index.</param>
    internal sealed record SimSavedScene(Guid Uuid, string Name, int NextItemId, IReadOnlyList<SimSavedItem> Items);

    /// <summary>
    /// What OBS Studio keeps of a scene collection that is not the current one: the file that it wrote when it left the collection, and read
    /// when it comes back. Only what was saved is there: no scene that was removed and kept alive, no input that was removed, and no
    /// lag of the list of scenes.
    /// </summary>
    /// <param name="Name">The name of the collection.</param>
    /// <param name="Scenes">The scenes of the list of scenes, in the order of the list.</param>
    /// <param name="Inputs">The inputs, in the order in which they were created.</param>
    /// <param name="ProgramSceneUuid">The UUID of the program scene.</param>
    internal sealed record SimSavedCollection(string Name, IReadOnlyList<SimSavedScene> Scenes, IReadOnlyList<SimSavedInput> Inputs, Guid ProgramSceneUuid);

    /// <summary>
    /// What OBS Studio held when it began to change its scene collection, and what it held when it had loaded the other: the events of a switch say nothing of what
    /// is in the collection that is loaded, so a replay of the events (<see cref="SimReplay"/>) starts again from the second.
    /// </summary>
    /// <param name="Left">What the collection that was left held when OBS Studio began to leave it.</param>
    /// <param name="Loaded">What the collection that was loaded held when it was loaded.</param>
    internal sealed record SimSwitchRecord(SimSnapshot Left, SimSnapshot Loaded);

    /// <summary>Saving a scene collection when OBS Studio leaves it, and loading one when it comes back.</summary>
    internal sealed partial class SimWorld
    {
        // Loads a collection from what was saved (Load).
        private SimWorld(SimObs obs, SimSavedCollection saved)
        {
            _obs = obs;
            Name = saved.Name;

            foreach (var input in saved.Inputs)
            {
                var loaded = new SimInput(input.Uuid, input.Name, NextSerial(), input.Kind, (JsonObject)input.Settings.DeepClone())
                {
                    Muted = input.Muted,
                    Volume = input.Volume,
                    Pins = input.Pins,
                    Balance = input.Balance,
                    SyncOffset = input.SyncOffset,
                    MonitorType = input.MonitorType,
                    Tracks = input.Tracks,
                };
                _byUuid[loaded.UuidText] = loaded;
                _inputs.Add(loaded);
            }

            // The list has the newest scene first, so the oldest scene gets the lowest number.
            var scenes = new SimScene[saved.Scenes.Count];
            for (var index = scenes.Length - 1; index >= 0; index--)
            {
                var entry = saved.Scenes[index];
                scenes[index] = new SimScene(entry.Uuid, entry.Name, NextSerial()) { CreatedAt = Now, NextItemId = entry.NextItemId };
                _byUuid[scenes[index].UuidText] = scenes[index];
            }

            for (var index = 0; index < scenes.Length; index++)
            {
                var scene = scenes[index];
                foreach (var entry in saved.Scenes[index].Items)
                {
                    var source = _byUuid[entry.SourceUuid.ToString("D")];
                    scene.Items.Add(new SimItem(entry.Id, scene, source) { Index = scene.Items.Count, Enabled = entry.Enabled, Locked = entry.Locked, Transform = entry.Transform.Clone(), BlendMode = entry.BlendMode });
                    source.References++;
                }

                _liveScenes.Add(scene);
                _uiList.Add(scene);
            }

            _program = scenes.First(scene => scene.Uuid == saved.ProgramSceneUuid);
            InitializeActivity();
        }

        /// <summary>
        /// Takes what OBS Studio writes to the file of the collection when it leaves it: the scenes of the list, the inputs that were not removed and that a
        /// scene item or a pin keeps, and the scene items that show them. A scene that was removed and is still alive, an input that was removed and is
        /// still alive, and the scene items that show them, are not saved: they are gone when the collection is loaded again.
        /// </summary>
        /// <remarks>
        /// ASSUMPTION: this is what obs_save_sources does and obs_load_sources undoes (a scene item whose source is not in the file is not loaded, and a source
        /// that nothing refers to is destroyed after the load), and nothing else of what the state holds (the lag of the list of scenes, a transition that
        /// runs) survives. Leaving a collection that has a removed input that is alive, and coming back, on a real OBS Studio, would confirm it.
        /// The ID that the next item of a scene gets is kept; OBS Studio may take it from the highest ID of the items that it loads, so that the ID of the last
        /// item that was removed can come back. Nothing that reads the simulation relies on either: everything is read again after a switch.
        /// </remarks>
        public SimSavedCollection Save()
        {
            var references = new Dictionary<Guid, int>();
            var scenes = new List<SimSavedScene>();
            foreach (var scene in _liveScenes)
            {
                var items = new List<SimSavedItem>();
                foreach (var item in scene.Items)
                {
                    if (item.Source.Removed)
                    {
                        continue;
                    }

                    items.Add(new SimSavedItem(item.Id, item.Source.Uuid, item.Enabled, item.Locked, item.Transform.Clone(), item.BlendMode));
                    references[item.Source.Uuid] = references.GetValueOrDefault(item.Source.Uuid) + 1;
                }

                scenes.Add(new SimSavedScene(scene.Uuid, scene.Name, scene.NextItemId, items));
            }

            var inputs = _inputs
                .Where(input => !input.Removed && (input.Pins > 0 || references.GetValueOrDefault(input.Uuid) > 0))
                .Select(input => new SimSavedInput(input.Uuid, input.Name, input.Kind, (JsonObject)input.Settings.DeepClone(), input.Muted, input.Volume, input.Pins, input.Balance, input.SyncOffset, input.MonitorType, input.Tracks))
                .ToArray();
            return new SimSavedCollection(Name, scenes, inputs, _program.Uuid);
        }

        /// <summary>Loads a scene collection from what was saved when it was left.</summary>
        /// <param name="obs">The simulated OBS Studio that the collection is in.</param>
        /// <param name="saved">What was saved.</param>
        public static SimWorld Load(SimObs obs, SimSavedCollection saved)
        {
            return new SimWorld(obs, saved);
        }

        /// <summary>
        /// Emits the events of the sources that were created while the collection loaded: the inputs, the scenes (the oldest first) and the scene items
        /// that were put in them, and the list of scenes. A server that is ready does not send them (see <see cref="SimQuirks.CreationEventsDuringSwitch"/>).
        /// </summary>
        /// <remarks>ASSUMPTION: the order is a guess; the events of a collection that loads, on a server that sends them, were not measured.</remarks>
        public void EmitLoadEvents()
        {
            foreach (var input in _inputs)
            {
                Emit("InputCreated", InputCreatedData(input));
            }

            foreach (var scene in _liveScenes.AsEnumerable().Reverse())
            {
                Emit("SceneCreated", SceneCreatedData(scene));
            }

            foreach (var scene in _liveScenes.AsEnumerable().Reverse())
            {
                foreach (var item in scene.Items)
                {
                    Emit("SceneItemCreated", Obj(
                        ("sceneItemId", item.Id),
                        ("sceneItemIndex", item.Index),
                        ("sceneName", scene.Name),
                        ("sceneUuid", scene.UuidText),
                        ("sourceName", item.Source.Name),
                        ("sourceUuid", item.Source.UuidText)));
                }
            }

            Emit("SceneListChanged", Obj(("scenes", SceneListJson(forEvent: true))));
        }
    }
}
