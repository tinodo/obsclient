namespace OBSStudioClient.Model.Engine
{
    using OBSStudioClient.Enums;

    internal sealed partial class ModelEngine
    {
        /// <summary>Gets the scenes and groups that the model holds, in no particular order. Only for the thread that runs the engine.</summary>
        public Dictionary<Guid, SceneNode>.ValueCollection SceneNodes => this._scenes.Values;

        /// <summary>Gets the inputs that the model holds, in no particular order. Only for the thread that runs the engine.</summary>
        public Dictionary<Guid, InputNode>.ValueCollection InputNodes => this._inputs.Values;

        /// <summary>Gets the UUID of the program scene, or null while it is not known.</summary>
        public Guid? ProgramSceneUuid => this._programUuid.Value;

        /// <summary>Gets the UUID of the preview scene, or null while studio mode is off (or it is not known yet).</summary>
        public Guid? PreviewSceneUuid => this._previewUuid.Value;

        /// <summary>Gets the current scene transition, or null while it is not known (or the scene collection has none).</summary>
        public TransitionInfo? Transition => this._transition.Value;

        /// <summary>Gets the profiles of OBS Studio and which one is current, or null while they are not known (or OBS Studio refuses to list them).</summary>
        public NameList? Profiles => this._profiles.Value;

        /// <summary>Gets the scene collections of OBS Studio and which one is loaded, or null while they are not known (or OBS Studio refuses to list them).</summary>
        public NameList? SceneCollections => this._collections.Value;

        /// <summary>Gets the canvases of OBS Studio, or null while they are not known (or OBS Studio refuses to list them: obs-websocket before 5.7.0).</summary>
        public CanvasList? Canvases => this._canvases.Value;

        /// <summary>Gets the duration that OBS Studio gives a transition that has one, in milliseconds, or null until OBS Studio has said.</summary>
        public int? TransitionDuration => this._transitionDuration.Value;

        /// <summary>Gets whether a transition is running: from the event that says that it started to the one that says that its video ended.</summary>
        public bool TransitionRunning => this._transitionRunning.Value;

        /// <summary>Gets how many transitions the events said started, since the engine was made.</summary>
        public long TransitionStarts => this._transitionStarts;

        /// <summary>Gets whether studio mode is on.</summary>
        public bool StudioModeEnabled => this._studioMode.Value;

        /// <summary>Gets whether OBS Studio said that it is exiting, on the connection that is the latest.</summary>
        public bool Exiting => this._exiting.Value;

        /// <summary>Gets whether the model holds a scene item: it was made, and was not removed. Only for the thread that runs the engine.</summary>
        /// <param name="sceneUuid">The UUID of the scene that has the item.</param>
        /// <param name="itemId">The id of the item.</param>
        public bool HasItem(Guid sceneUuid, int itemId)
        {
            return this._scenes.TryGetValue(sceneUuid, out var scene) && scene.Items.ContainsKey(itemId);
        }

        /// <summary>Gets what the model holds about an output. Only for the thread that runs the engine.</summary>
        /// <param name="kind">The output.</param>
        public OutputNode Output(ObsOutputKind kind)
        {
            return this._outputs[(int)kind];
        }

        /// <summary>
        /// Gets whether the model needs an event of this type, when the application asked to follow everything that OBS Studio sends very often (see <see cref="Wants"/> for what it needs for what it was
        /// asked). The events that it does not need are not even passed to it.
        /// </summary>
        /// <param name="eventType">The type of the event.</param>
        public static bool Handles(EventType eventType)
        {
            return Handlers.ContainsKey(eventType);
        }

        /// <summary>
        /// Gets whether the engine acts on an event of this type: it handles it, and when OBS Studio sends it very often (see <see cref="HighVolumeOf"/>) the application asked to follow what it is about.
        /// The host does not pass the others to it, and it ignores them when it is.
        /// </summary>
        /// <param name="eventType">The type of the event.</param>
        public bool Wants(EventType eventType)
        {
            return Handlers.ContainsKey(eventType) && (HighVolumeOf(eventType) is not { } part || this.Follows(part));
        }

        /// <summary>Gets the part of what OBS Studio sends very often that an event is about, or null for an event that is not sent very often.</summary>
        /// <param name="eventType">The type of the event.</param>
        public static ObsHighVolume? HighVolumeOf(EventType eventType)
        {
            return eventType switch
            {
                EventType.SceneItemTransformChanged => ObsHighVolume.Transforms,
                EventType.InputActiveStateChanged or EventType.InputShowStateChanged => ObsHighVolume.Activity,
                _ => null,
            };
        }

        private bool Follows(ObsHighVolume part)
        {
            return this._options.HighVolume.HasFlag(part);
        }
    }
}
