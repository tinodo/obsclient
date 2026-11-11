namespace OBSStudioClient.Model
{
    using System.ComponentModel;
    using OBSStudioClient.Classes;

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
    public sealed partial class ObsSceneItem : INotifyPropertyChanged
    {
        private int _index;
        private bool _enabled;
        private bool _locked;
        private bool _removed;
        private SceneItemTransform? _transform;

        internal ObsSceneItem(ObsScene scene, ObsSource source, int id, int index, bool enabled, bool locked, SceneItemTransform? transform = null)
        {
            this.Scene = scene;
            this.Source = source;
            this.Id = id;
            this._index = index;
            this._enabled = enabled;
            this._locked = locked;
            this._transform = transform;
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

        /// <summary>
        /// Gets the transform of the item: where it is, how large it is shown, how it is rotated, cropped and bounded. It is null unless the model was asked to follow the transforms
        /// (<see cref="ObsModelOptions.HighVolume"/> with <see cref="ObsHighVolume.Transforms"/>): OBS Studio sends an event for every step of a drag, so the model does not ask for them unless they are wanted. To ask OBS Studio
        /// once, whatever the options are, use <see cref="GetTransformAsync"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The value is current, and the object is a copy that does not change: when the transform changes the property gets another object (and PropertyChanged and <see cref="ObsModel.Changed"/> tell). OBS Studio
        /// sends the transform again when something else about the item changes (a blend mode that is set, for one), so the model only tells when the transform is not the one that it held.
        /// </para>
        /// <para>
        /// When several programs change the same item at the same moment, the events can come in another order than the changes were made (measured with OBS Studio 33.0.0: two of fifty changes that were
        /// sent at once), and the model shows the last that came: <see cref="GetTransformAsync"/> says what OBS Studio holds.
        /// </para>
        /// <para>
        /// OBS Studio 33.0.0 announces a change of the position, the scale, the rotation, the alignment and the bounds at once, and a change of the crop and of the size of the source (which changes the size of the
        /// item) only for a scene that it renders: the program scene, the preview scene of studio mode, a scene that a projector shows. For another scene it announces them when the scene is rendered, and until then the
        /// transform that the model shows of an item in it has the crop and the size from before. The changes that the model makes itself (<see cref="SetTransformAsync"/>, <see cref="DuplicateAsync"/>) read the scene again
        /// and do not depend on it; <see cref="GetTransformAsync"/> says what OBS Studio holds, whatever the scene is.
        /// </para>
        /// </remarks>
        public SceneItemTransform? Transform => Volatile.Read(ref this._transform);

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

        internal bool SetTransform(SceneItemTransform? transform)
        {
            if (Equals(Volatile.Read(ref this._transform), transform))
            {
                return false;
            }

            Volatile.Write(ref this._transform, transform);
            return true;
        }

        internal void RaisePropertyChanged(string propertyName)
        {
            this.Scene.Model.Raise(this.PropertyChanged, this, propertyName);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"Scene item {this.Id} of '{this.Scene.Name}': '{this.Source.Name}'";
        }
    }
}
