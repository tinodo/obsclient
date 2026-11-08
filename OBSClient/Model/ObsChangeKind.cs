namespace OBSStudioClient.Model
{
    /// <summary>
    /// What kind of change an <see cref="ObsChangedEventArgs"/> describes.
    /// </summary>
    public enum ObsChangeKind
    {
        /// <summary>
        /// A scene, an input or a scene item appeared. An input is announced when it is complete (its audio state is known), and a scene item when its index
        /// and its state are known. A scene is announced when OBS Studio creates it, and its scene items are read after that: each is announced when it is
        /// known, so the <see cref="ObsScene.Items"/> of a scene that a handler is told of can be incomplete. Call <see cref="ObsModel.WaitUntilSettledAsync"/>
        /// when you need all of it.
        /// </summary>
        Added,

        /// <summary>
        /// A scene, an input or a scene item is gone.
        /// </summary>
        Removed,

        /// <summary>
        /// A property of an object (or of the <see cref="ObsModel"/>) changed. The name of the property, its old and its new value are given.
        /// </summary>
        PropertyChanged,

        /// <summary>
        /// The state of a scene or an input changed: OBS Studio removed one that scene items still use, and it is now <see cref="ObsObjectState.Detached"/>; or the model took it for
        /// removed and a read showed that OBS Studio has it as a live object (a scene that its list did not have in time: the list is made from the user interface of OBS Studio, which can be
        /// busy), and it is <see cref="ObsObjectState.Live"/> again.
        /// </summary>
        StateChanged,

        /// <summary>
        /// The model corrected itself: it held something that a read showed not to exist, or it took a scene for removed (see <see cref="StateChanged"/>) and a read showed that it is there.
        /// </summary>
        Repaired,
    }
}
