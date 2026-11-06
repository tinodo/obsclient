namespace OBSStudioClient.Studio
{
    /// <summary>
    /// A scene of OBS Studio.
    /// </summary>
    /// <remarks>
    /// A group of OBS Studio is a scene too (<see cref="IsGroup"/>): it is not in <see cref="ObsStudio.Scenes"/>, but it is the
    /// <see cref="ObsSceneItem.Source"/> of the scene item that shows it.
    /// </remarks>
    public sealed class ObsScene : ObsSource
    {
        private ObsSceneItems _items;
        private int _index = -1;

        internal ObsScene(ObsStudio studio, Guid uuid, string name, ObsObjectState state, bool isGroup)
            : base(studio, uuid, name, state)
        {
            this.IsGroup = isGroup;
            this._items = new ObsSceneItems(this, [], studio.IgnoreCase);
        }

        /// <summary>
        /// Gets whether the scene is a group.
        /// </summary>
        public bool IsGroup { get; }

        /// <summary>
        /// Gets the position of the scene in the list of scenes of OBS Studio: 0 is the first, as in <see cref="ObsStudio.Scenes"/>. It is -1 for a
        /// scene that is not in that list: a group, and a scene that is not <see cref="ObsObjectState.Live"/>.
        /// </summary>
        public int Index => Volatile.Read(ref this._index);

        /// <summary>
        /// Gets whether the scene is the program scene: the one that is on the air.
        /// </summary>
        public bool IsProgram => ReferenceEquals(this.Studio.ProgramScene, this);

        /// <summary>
        /// Gets the scene items of the scene, from the bottom (index 0) to the top.
        /// </summary>
        /// <remarks>
        /// The items of a scene that was removed but that OBS Studio keeps alive (<see cref="ObsSource.State"/> is <see cref="ObsObjectState.Detached"/>) are available too.
        /// </remarks>
        public ObsSceneItems Items => Volatile.Read(ref this._items);

        // Written by one thread only, the one that runs the model, and read by any.
        internal bool SetIndex(int index)
        {
            if (Volatile.Read(ref this._index) == index)
            {
                return false;
            }

            Volatile.Write(ref this._index, index);
            return true;
        }

        internal void SetItems(ObsSceneItems items)
        {
            Volatile.Write(ref this._items, items);
        }
    }
}
