namespace OBSStudioClient.Studio
{
    /// <summary>
    /// What OBS Studio has done with a scene or an input.
    /// </summary>
    /// <remarks>
    /// OBS Studio counts references. When it removes a scene or an input while a scene item still uses it, it does not destroy it: it keeps
    /// it alive, with its scene items, until the last scene item that uses it is removed. The model follows that, and hides nothing.
    /// </remarks>
    public enum ObsObjectState
    {
        /// <summary>
        /// The object exists, and has not been removed.
        /// </summary>
        Live,

        /// <summary>
        /// OBS Studio removed the object, and keeps it alive because scene items still use it. It is not in <see cref="ObsStudio.Scenes"/> or
        /// <see cref="ObsStudio.Inputs"/> any more, but its scene items are still in their scenes (<see cref="ObsSceneItem.Source"/> gets to it), and
        /// the name of a removed input stays taken until the last scene item that uses it is removed.
        /// </summary>
        Detached,

        /// <summary>
        /// The object does not exist any more.
        /// </summary>
        Gone,
    }
}
