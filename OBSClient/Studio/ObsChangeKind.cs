namespace OBSStudioClient.Studio
{
    /// <summary>
    /// What kind of change an <see cref="ObsChangedEventArgs"/> describes.
    /// </summary>
    public enum ObsChangeKind
    {
        /// <summary>
        /// A scene, an input or a scene item appeared. An input is announced when it is complete (its audio state is known), and a scene item when its index
        /// and its state are known. A scene is announced when OBS Studio creates it, and its scene items are read after that: each is announced when it is
        /// known, so the <see cref="ObsScene.Items"/> of a scene that a handler is told of can be incomplete. Call <see cref="ObsStudio.WaitUntilSettledAsync"/>
        /// when you need all of it.
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
