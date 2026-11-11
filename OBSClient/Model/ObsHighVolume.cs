namespace OBSStudioClient.Model
{
    /// <summary>
    /// What OBS Studio sends very often, and that an <see cref="ObsModel"/> follows only when it is asked to (see <see cref="ObsModelOptions.HighVolume"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// OBS Studio sends these events to every client that is subscribed to them, and what a client that is not interested does with them is still work for OBS Studio and for the
    /// connection: dragging a scene item in the preview is sixty events a second, the levels of the audio are twenty messages a second for as long as the connection lasts, and every program that listens
    /// adds its own copy. So the model subscribes to them only for what you ask for, and what it keeps of them (<see cref="ObsSceneItem.Transform"/>, <see cref="ObsInput.IsActive"/>,
    /// <see cref="ObsInput.IsShowing"/> and <see cref="ObsInput.Levels"/>) is <c>null</c> as long as you did not.
    /// </para>
    /// <para>
    /// What you can ask OBS Studio for by a call does not need any of this: <see cref="ObsSceneItem.GetTransformAsync"/> and <see cref="ObsSource.GetActivityAsync"/> are always there.
    /// </para>
    /// </remarks>
    [Flags]
    public enum ObsHighVolume
    {
        /// <summary>None of them: the default.</summary>
        None = 0,

        /// <summary>
        /// The transform of every scene item (<see cref="ObsSceneItem.Transform"/>): where it is, how large, how it is cropped. OBS Studio sends an event for every step of a drag, and also when
        /// the size of a source changes. OBS Studio 33.0.0 announces a crop and the size of a source only for a scene that it renders (the program scene, the preview scene of studio mode, a scene that a
        /// projector shows): for another scene it announces them when the scene is rendered, so the transform that the model shows of an item in such a scene can be behind until then.
        /// </summary>
        Transforms = 1,

        /// <summary>
        /// Whether each input is on the air and whether it is shown (<see cref="ObsInput.IsActive"/> and <see cref="ObsInput.IsShowing"/>): OBS Studio sends an event for the inputs of a scene each time that the program scene, the
        /// preview scene, a transition, or a scene item that shows them changes. The model reads the state of every input once (<c>GetSourceActive</c>), and follows the events. OBS Studio says nothing of a scene, and
        /// nothing of an input that it destroys.
        /// </summary>
        Activity = 2,

        /// <summary>
        /// The levels of the audio of the inputs that are on the air (<see cref="ObsInput.Levels"/> and <see cref="ObsModel.AudioLevels"/>): OBS Studio sends a message every 50 milliseconds, also when nothing
        /// is playing, and has every input that is on the air report its levels for as long as a client is subscribed. The model keeps the newest levels, and tells the application of them without ever
        /// holding up OBS Studio, or the rest of the model: a handler that is slower than OBS Studio gets the newest levels and not all of them.
        /// </summary>
        AudioLevels = 4,

        /// <summary>All of them.</summary>
        All = Transforms | Activity | AudioLevels,
    }
}
