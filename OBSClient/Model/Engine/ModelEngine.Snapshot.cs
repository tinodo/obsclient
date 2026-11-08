namespace OBSStudioClient.Model.Engine
{
    /// <summary>A scene item, as the model holds it.</summary>
    internal sealed record EngineItem(int Id, Guid SourceUuid, int Index, bool Enabled, bool Locked);

    /// <summary>A scene, as the model holds it.</summary>
    internal sealed record EngineScene(Guid Uuid, string Name, int Index, SourceState State, IReadOnlyList<EngineItem> Items);

    /// <summary>An input, as the model holds it.</summary>
    internal sealed record EngineInput(Guid Uuid, string Name, string Kind, bool HasAudio, bool Muted, float VolumeMul, float VolumeDb, SourceState State);

    /// <summary>A filter of a scene or an input, as the model holds it.</summary>
    internal sealed record EngineFilter(long Id, string Name, string Kind, int Index, bool Enabled);

    /// <summary>
    /// What the model holds, as plain values, for the tests.
    /// </summary>
    /// <param name="Scenes">The live scenes, in the order of the list of scenes.</param>
    /// <param name="DetachedScenes">The scenes that OBS removed and keeps alive, by UUID.</param>
    /// <param name="Inputs">The inputs that are alive (a Detached one included), by UUID.</param>
    /// <param name="ProgramSceneUuid">The UUID of the program scene.</param>
    /// <param name="StudioMode">Whether studio mode is on.</param>
    /// <param name="State">The state of the connection of the engine.</param>
    /// <param name="Incomplete">The number of things that the model holds but does not know completely.</param>
    internal sealed record EngineSnapshot(
        IReadOnlyList<EngineScene> Scenes,
        IReadOnlyList<EngineScene> DetachedScenes,
        IReadOnlyList<EngineInput> Inputs,
        Guid? ProgramSceneUuid,
        bool StudioMode,
        ModelEngineState State,
        int Incomplete);

    internal sealed partial class ModelEngine
    {
        /// <summary>Returns what the model holds, as plain values.</summary>
        public EngineSnapshot Snapshot()
        {
            static IReadOnlyList<EngineItem> ItemsOf(SceneNode scene)
            {
                return scene.Items.Values
                    .Where(item => item.Known)
                    .OrderBy(item => item.Index.Value)
                    .ThenBy(item => item.Id)
                    .Select(item => new EngineItem(item.Id, item.SourceUuid, item.Index.Value, item.Enabled.Value, item.Locked.Value))
                    .ToArray();
            }

            var live = this._scenes.Values
                .Where(scene => scene.State == SourceState.Live && !scene.IsGroup)
                .OrderBy(scene => scene.Index.Value)
                .ThenBy(scene => scene.Uuid)
                .Select(scene => new EngineScene(scene.Uuid, scene.Name.Value, scene.Index.Value, scene.State, ItemsOf(scene)))
                .ToArray();
            var detached = this._scenes.Values
                .Where(scene => scene.State == SourceState.Detached)
                .OrderBy(scene => scene.Uuid)
                .Select(scene => new EngineScene(scene.Uuid, scene.Name.Value, scene.Index.Value, scene.State, ItemsOf(scene)))
                .ToArray();
            var inputs = this._inputs.Values
                .Where(input => input.IsComplete)
                .OrderBy(input => input.Uuid)
                .Select(input => new EngineInput(input.Uuid, input.Name.Value, input.Kind, input.HasAudio, input.Muted.Value, input.VolumeMul.Value, input.VolumeDb.Value, input.State))
                .ToArray();
            return new EngineSnapshot(live, detached, inputs, this._programUuid.Value, this._studioMode.Value, this._state, this.PendingWork());
        }

        /// <summary>Returns the filters that the model holds of a scene or an input, from the first, as plain values; none when the model does not hold the source.</summary>
        /// <param name="source">The UUID of the scene or the input.</param>
        public IReadOnlyList<EngineFilter> FiltersOf(Guid source)
        {
            return this.SourceByUuid(source)?.Filters
                .OrderBy(filter => filter.Index.Value)
                .ThenBy(filter => filter.Name.Value, StringComparer.Ordinal)
                .Select(filter => new EngineFilter(filter.Id, filter.Name.Value, filter.Kind, filter.Index.Value, filter.Enabled.Value))
                .ToArray() ?? [];
        }

        /// <summary>
        /// Checks that what the model holds is consistent with itself: that the references of a source are the scene items that use it and no others, and that
        /// nothing that was destroyed is still held. It returns what is not. It is for the tests (the walks call it each time that they compare the model with OBS
        /// Studio; that the model has settled, with no removed source left waiting for a decision, is part of <see cref="IsSettled"/>).
        /// </summary>
        /// <returns>What is inconsistent, or nothing.</returns>
        public IReadOnlyList<string> CheckConsistency()
        {
            var problems = new List<string>();
            var counted = new Dictionary<Guid, int>();
            foreach (var source in this._scenes.Values.Cast<SourceNode>().Concat(this._inputs.Values))
            {
                foreach (var group in source.Filters.GroupBy(filter => filter.Name.Value, StringComparer.Ordinal).Where(group => group.Count() > 1))
                {
                    problems.Add($"The source {source.Uuid:D} \"{source.Name.Value}\" has {group.Count()} filters named \"{group.Key}\".");
                }
            }
            foreach (var scene in this._scenes.Values)
            {
                if (scene.State == SourceState.Gone)
                {
                    problems.Add($"The scene {scene.Uuid:D} \"{scene.Name.Value}\" was destroyed, and is still held.");
                }

                foreach (var item in scene.Items.Values)
                {
                    counted[item.SourceUuid] = counted.GetValueOrDefault(item.SourceUuid) + 1;
                    if (!this._refs.TryGetValue(item.SourceUuid, out var references) || !references.Contains(item.Key))
                    {
                        problems.Add($"The scene item {item.Id} of the scene {scene.Uuid:D} is not among the references of its source {item.SourceUuid:D}.");
                    }
                }
            }

            foreach (var (source, references) in this._refs)
            {
                var items = counted.GetValueOrDefault(source);
                if (references.Count == 0 || references.Count != items)
                {
                    problems.Add($"The source {source:D} has {references.Count} references and {items} scene items that use it.");
                }
            }

            foreach (var input in this._inputs.Values.Where(input => input.State == SourceState.Gone))
            {
                problems.Add($"The input {input.Uuid:D} \"{input.Name.Value}\" was destroyed, and is still held.");
            }

            return problems;
        }
    }
}
