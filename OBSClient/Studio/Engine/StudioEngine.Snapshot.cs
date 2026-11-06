namespace OBSStudioClient.Studio.Engine
{
    /// <summary>A scene item, as the model holds it.</summary>
    internal sealed record EngineItem(int Id, Guid SourceUuid, int Index, bool Enabled, bool Locked);

    /// <summary>A scene, as the model holds it.</summary>
    internal sealed record EngineScene(Guid Uuid, string Name, int Index, SourceState State, IReadOnlyList<EngineItem> Items);

    /// <summary>An input, as the model holds it.</summary>
    internal sealed record EngineInput(Guid Uuid, string Name, string Kind, bool HasAudio, bool Muted, float VolumeMul, float VolumeDb, SourceState State);

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
        StudioEngineState State,
        int Incomplete);

    internal sealed partial class StudioEngine
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
    }
}
