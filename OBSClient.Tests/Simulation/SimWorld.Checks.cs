namespace OBSStudioClient.Tests.Simulation
{
    /// <summary>The snapshot of the state of a simulated OBS Studio, and the rules that its state has to follow.</summary>
    internal sealed partial class SimWorld
    {
        /// <summary>Lets go of the scenes that the user interface no longer lists: the ones removed longer ago than their lag.</summary>
        public void PruneUiList()
        {
            var now = Now;
            _uiList.RemoveAll(scene => scene.Removed && scene.RemovedAt!.Value + scene.Lag <= now);
        }

        /// <summary>Takes the snapshot: what OBS Studio really holds, without any lag.</summary>
        public SimSnapshot Snapshot()
        {
            var scenes = _liveScenes.Select((scene, index) => SnapshotOf(scene, index)).ToArray();
            var detached = DetachedScenes.Select(scene => SnapshotOf(scene, -1)).ToArray();
            var inputs = _inputs
                .Select(input => new SimInputSnapshot(
                    input.Uuid,
                    input.Name,
                    input.Kind.Id,
                    input.Kind.HasAudio,
                    input.Kind.HasAudio && input.Muted,
                    input.Kind.HasAudio ? input.Volume : 1.0,
                    input.Kind.HasAudio ? MulToDb(input.Volume) : 0.0,
                    input.Pins > 0,
                    input.Removed,
                    SimJson.Serialize(input.Settings),
                    input.Kind.HasAudio ? input.Balance : 0.5f,
                    input.Kind.HasAudio ? input.SyncOffset : 0,
                    input.Kind.HasAudio ? input.MonitorType : "OBS_MONITORING_TYPE_NONE",
                    input.Kind.HasAudio ? input.Tracks : 63,
                    input.Kind.IsMedia ? input.MediaState : null,
                    input.Active,
                    input.Showing))
                .ToArray();
            return new SimSnapshot(Name, scenes, detached, inputs, _program.Uuid, _studioMode ? _preview?.Uuid : null, _studioMode, TransitionName, TransitionDurationMilliseconds, TransitionRunning, Canvases: SnapshotCanvases());
        }

        /// <summary>
        /// Lists what is wrong with the state. The rules: the number of references of a source is the number of scene items that use it
        /// (plus the pins of an input); the indexes of the items of a scene are 0 to n-1 in order, and their IDs are unique; the names are
        /// unique for the scenes of the list and for the inputs that are alive; an input that is alive has a scene item or a pin; a scene that
        /// was removed and is alive is used by a scene item; a destroyed source is not used by anything; there are no scenes that hold each
        /// other; and the program scene is a scene of the list.
        /// </summary>
        public List<string> FindProblems()
        {
            var problems = new List<string>(_violations);
            var references = new Dictionary<string, int>(StringComparer.Ordinal);
            var alive = new List<SimScene>(_liveScenes);
            alive.AddRange(_detached);

            foreach (var scene in alive)
            {
                if (scene.Destroyed)
                {
                    problems.Add($"The destroyed scene \"{scene.Name}\" is still in the state.");
                }

                var ids = new HashSet<int>();
                for (var index = 0; index < scene.Items.Count; index++)
                {
                    var item = scene.Items[index];
                    if (item.Index != index)
                    {
                        problems.Add($"Item #{item.Id} of scene \"{scene.Name}\" has index {item.Index} but is at position {index}.");
                    }

                    if (!ids.Add(item.Id))
                    {
                        problems.Add($"Scene \"{scene.Name}\" has two items with ID {item.Id}.");
                    }

                    if (item.Id >= scene.NextItemId)
                    {
                        problems.Add($"Item #{item.Id} of scene \"{scene.Name}\" is not below the next ID ({scene.NextItemId}).");
                    }

                    if (!ReferenceEquals(item.Scene, scene))
                    {
                        problems.Add($"Item #{item.Id} of scene \"{scene.Name}\" says that it is in another scene.");
                    }

                    if (item.Source.Destroyed || !_byUuid.TryGetValue(item.Source.UuidText, out var known) || !ReferenceEquals(known, item.Source))
                    {
                        problems.Add($"Item #{item.Id} of scene \"{scene.Name}\" uses \"{item.Source.Name}\", which is not alive.");
                    }

                    references[item.Source.UuidText] = references.GetValueOrDefault(item.Source.UuidText) + 1;
                }
            }

            foreach (var scene in _liveScenes)
            {
                if (scene.Removed)
                {
                    problems.Add($"Scene \"{scene.Name}\" is in the list of scenes but was removed.");
                }

                if (!_byUuid.ContainsKey(scene.UuidText))
                {
                    problems.Add($"Scene \"{scene.Name}\" is in the list of scenes but not alive.");
                }
            }

            foreach (var group in _liveScenes.GroupBy(scene => scene.Name, StringComparer.Ordinal).Where(group => group.Count() > 1))
            {
                problems.Add($"{group.Count()} scenes of the list are named \"{group.Key}\".");
            }

            foreach (var scene in _detached)
            {
                if (!scene.Removed || scene.References <= 0)
                {
                    problems.Add($"Scene \"{scene.Name}\" is detached but removed={scene.Removed} and has {scene.References} references.");
                }

                if (!_byUuid.ContainsKey(scene.UuidText))
                {
                    problems.Add($"The detached scene \"{scene.Name}\" is not alive.");
                }
            }

            foreach (var group in _inputs.GroupBy(input => input.Name, StringComparer.Ordinal).Where(group => group.Count() > 1))
            {
                problems.Add($"{group.Count()} inputs that are alive are named \"{group.Key}\".");
            }

            foreach (var group in _canvases.Where(canvas => !canvas.Removed).GroupBy(canvas => canvas.Name, StringComparer.Ordinal).Where(group => group.Count() > 1))
            {
                problems.Add($"{group.Count()} canvases that are alive are named \"{group.Key}\".");
            }

            if (_canvases.Any(canvas => !canvas.Removed && string.Equals(canvas.Name, MainCanvasName, StringComparison.Ordinal)))
            {
                problems.Add($"A canvas that is not the main one is named \"{MainCanvasName}\".");
            }

            foreach (var canvas in _canvases.Where(canvas => canvas.IsMain))
            {
                problems.Add($"The canvas \"{canvas.Name}\" has the flag of the main canvas, and there is only one.");
            }

            foreach (var input in _inputs)
            {
                if (input.Destroyed)
                {
                    problems.Add($"The destroyed input \"{input.Name}\" is still in the state.");
                }

                if (input.References == 0 && input.Pins == 0)
                {
                    problems.Add($"Input \"{input.Name}\" is alive without a scene item or a pin.");
                }

                if (input.Pins < 0)
                {
                    problems.Add($"Input \"{input.Name}\" has {input.Pins} pins.");
                }

                if (!_byUuid.TryGetValue(input.UuidText, out var known) || !ReferenceEquals(known, input))
                {
                    problems.Add($"Input \"{input.Name}\" is not in the table of UUIDs.");
                }
            }

            foreach (var (uuid, source) in _byUuid)
            {
                var expected = references.GetValueOrDefault(uuid);
                if (source.References != expected)
                {
                    problems.Add($"\"{source.Name}\" says it has {source.References} references, and {expected} scene items use it.");
                }

                if (source.Destroyed)
                {
                    problems.Add($"The destroyed source \"{source.Name}\" is in the table of UUIDs.");
                }

                if (source is SimScene scene && !_liveScenes.Contains(scene) && !_detached.Contains(scene))
                {
                    problems.Add($"Scene \"{scene.Name}\" is alive but neither in the list of scenes nor detached.");
                }

                if (source is SimInput input && !_inputs.Contains(input))
                {
                    problems.Add($"Input \"{input.Name}\" is in the table of UUIDs but not in the list of inputs.");
                }
            }

            var finished = new HashSet<SimScene>();
            foreach (var scene in alive)
            {
                if (HasLoop(scene, new HashSet<SimScene>(), finished))
                {
                    problems.Add($"Scene \"{scene.Name}\" is nested in itself.");
                }
            }

            if (_liveScenes.Count > 0 && !_liveScenes.Contains(_program))
            {
                problems.Add($"The program scene \"{_program.Name}\" is not a scene of the list.");
            }

            if (_transition is { } running && running.Cancelled)
            {
                problems.Add("The running transition was cancelled.");
            }

            problems.AddRange(FindActivityProblems());
            return problems;
        }

        // Depth-first search for a scene that holds itself, through any number of scenes. A scene that was searched without finding one is
        // not searched again.
        private static bool HasLoop(SimScene scene, HashSet<SimScene> onPath, HashSet<SimScene> finished)
        {
            if (finished.Contains(scene))
            {
                return false;
            }

            if (!onPath.Add(scene))
            {
                return true;
            }

            foreach (var item in scene.Items)
            {
                if (item.Source is SimScene nested && HasLoop(nested, onPath, finished))
                {
                    return true;
                }
            }

            onPath.Remove(scene);
            finished.Add(scene);
            return false;
        }

        private static SimSceneSnapshot SnapshotOf(SimScene scene, int index)
        {
            var items = scene.Items
                .Select(item => new SimItemSnapshot(item.Id, item.Source.Uuid, item.Source.Name, item.Index, item.Enabled, item.Locked, item.Source.IsScene, SimJson.Serialize(TransformJson(item)), item.BlendMode))
                .ToArray();
            return new SimSceneSnapshot(scene.Uuid, scene.Name, index, items);
        }
    }
}
