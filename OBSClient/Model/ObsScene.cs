namespace OBSStudioClient.Model
{
    /// <summary>
    /// A scene of OBS Studio.
    /// </summary>
    /// <remarks>
    /// A group of OBS Studio is a scene too (<see cref="IsGroup"/>): it is not in <see cref="ObsModel.Scenes"/>, but it is the
    /// <see cref="ObsSceneItem.Source"/> of the scene item that shows it.
    /// </remarks>
    public sealed partial class ObsScene : ObsSource
    {
        private ObsSceneItems _items;
        private int _index = -1;
        private int _removalUnverified;

        internal ObsScene(ObsModel model, Guid uuid, string name, ObsObjectState state, bool isGroup)
            : base(model, uuid, name, state)
        {
            this.IsGroup = isGroup;
            this._items = new ObsSceneItems(this, [], model.IgnoreCase);
        }

        /// <summary>
        /// Gets whether the scene is a group.
        /// </summary>
        public bool IsGroup { get; }

        /// <summary>
        /// Gets the position of the scene in the list of scenes of OBS Studio: 0 is the first, as in <see cref="ObsModel.Scenes"/>. It is -1 for a
        /// scene that is not in that list: a group, and a scene that is not <see cref="ObsObjectState.Live"/>.
        /// </summary>
        public int Index => Volatile.Read(ref this._index);

        /// <summary>
        /// Gets whether the scene is the program scene: the one that is on the air.
        /// </summary>
        public bool IsProgram => ReferenceEquals(this.Model.ProgramScene, this);

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

        // Whether the model took the scene for removed because a list of scenes did not have it, while no event of OBS Studio removed it: it knew the scene as a live scene, and the list of OBS Studio
        // is made from its user interface, which can be busy for seconds (23 were seen), so OBS Studio can still have the scene as a live scene. Written by the publisher together with the state.
        internal bool RemovalUnverified => Volatile.Read(ref this._removalUnverified) != 0;

        internal void SetRemovalUnverified(bool unverified)
        {
            Volatile.Write(ref this._removalUnverified, unverified ? 1 : 0);
        }
    }
}
