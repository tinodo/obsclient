namespace OBSStudioClient.Studio
{
    /// <summary>
    /// What kind of change an <see cref="ObsChangedEventArgs"/> describes.
    /// </summary>
    public enum ObsChangeKind
    {
        /// <summary>
        /// A scene, an input or a scene item appeared. It is complete: everything about it is known.
        /// </summary>
        Added,

        /// <summary>
        /// A scene, an input or a scene item is gone.
        /// </summary>
        Removed,

        /// <summary>
        /// A property of an object (or of the <see cref="ObsStudio"/>) changed. The name of the property, its old and its new value are given.
        /// </summary>
        PropertyChanged,

        /// <summary>
        /// OBS Studio removed a scene or an input that scene items still use: the object is now <see cref="ObsObjectState.Detached"/>.
        /// </summary>
        StateChanged,

        /// <summary>
        /// The model corrected itself: it held something that a read showed not to exist.
        /// </summary>
        Repaired,
    }
}
