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

        /// <summary>Gets whether studio mode is on.</summary>
        public bool StudioModeEnabled => this._studioMode.Value;

        /// <summary>Gets what the model holds about an output. Only for the thread that runs the engine.</summary>
        /// <param name="kind">The output.</param>
        public OutputNode Output(ObsOutputKind kind)
        {
            return this._outputs[(int)kind];
        }

        /// <summary>
        /// Gets whether the model needs an event of this type. The events that it does not need are not even passed to it.
        /// </summary>
        /// <param name="eventType">The type of the event.</param>
        public static bool Handles(EventType eventType)
        {
            return Handlers.ContainsKey(eventType);
        }
    }
}
