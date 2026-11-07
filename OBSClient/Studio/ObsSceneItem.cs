namespace OBSStudioClient.Studio
{
    using System.ComponentModel;

    /// <summary>
    /// A scene item of OBS Studio: a source (an input, or another scene) as it is shown in one scene.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The same source can be shown in a scene more than once; each copy is a scene item with an id of its own. The id is unique within its
    /// scene, and is never used again.
    /// </para>
    /// <para>
    /// The properties can be read from any thread, and <see cref="INotifyPropertyChanged"/> tells when a value changed (see <see cref="ObsSource"/>).
    /// A reference to a scene item that was removed stays valid: <see cref="IsRemoved"/> is true.
    /// </para>
    /// </remarks>
    public sealed class ObsSceneItem : INotifyPropertyChanged
    {
        private int _index;
        private bool _enabled;
        private bool _locked;
        private bool _removed;

        internal ObsSceneItem(ObsScene scene, ObsSource source, int id, int index, bool enabled, bool locked)
        {
            this.Scene = scene;
            this.Source = source;
            this.Id = id;
            this._index = index;
            this._enabled = enabled;
            this._locked = locked;
        }

        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Gets the scene that contains the item (a group, for an item in a group).
        /// </summary>
        public ObsScene Scene { get; }

        /// <summary>
        /// Gets the source that the item shows: an <see cref="ObsInput"/> or an <see cref="ObsScene"/>.
        /// </summary>
        public ObsSource Source { get; }

        /// <summary>
        /// Gets the id of the item, which is unique within its scene and never used again.
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// Gets the position of the item in its scene: 0 is the bottom. The positions of the items of a scene are 0, 1, 2, and so on.
        /// </summary>
        public int Index => Volatile.Read(ref this._index);

        /// <summary>
        /// Gets whether the item is visible. The value is current: OBS Studio announces every change of it.
        /// </summary>
        public bool IsEnabled => Volatile.Read(ref this._enabled);

        /// <summary>
        /// Gets whether the item is locked. The value is current: OBS Studio announces every change of it.
        /// </summary>
        public bool IsLocked => Volatile.Read(ref this._locked);

        /// <summary>
        /// Gets whether the item was removed from its scene.
        /// </summary>
        public bool IsRemoved => Volatile.Read(ref this._removed);

        // The number of the pass of the publisher that last saw the item in the model: an item that a pass does not see is gone.
        internal int Pass { get; set; }

        // The values are written by one thread only, the one that runs the model, and read by any.
        internal bool SetIndex(int index)
        {
            if (Volatile.Read(ref this._index) == index)
            {
                return false;
            }

            Volatile.Write(ref this._index, index);
            return true;
        }

        internal bool SetEnabled(bool enabled)
        {
            if (Volatile.Read(ref this._enabled) == enabled)
            {
                return false;
            }

            Volatile.Write(ref this._enabled, enabled);
            return true;
        }

        internal bool SetLocked(bool locked)
        {
            if (Volatile.Read(ref this._locked) == locked)
            {
                return false;
            }

            Volatile.Write(ref this._locked, locked);
            return true;
        }

        internal bool SetRemoved()
        {
            if (Volatile.Read(ref this._removed))
            {
                return false;
            }

            Volatile.Write(ref this._removed, true);
            return true;
        }

        internal void RaisePropertyChanged(string propertyName)
        {
            this.Scene.Studio.Raise(this.PropertyChanged, this, propertyName);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"Scene item {this.Id} of '{this.Scene.Name}': '{this.Source.Name}'";
        }
    }
}
