namespace OBSStudioClient.Studio
{
    using System.ComponentModel;

    /// <summary>
    /// A scene or an input of OBS Studio: something that has a name and a UUID, and that scene items can show.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An object keeps its identity for as long as it exists, also across renames and reconnects: the UUID is what identifies it, the name
    /// is a label that can change. A reference to an object that was removed stays valid, and says so in <see cref="State"/>.
    /// </para>
    /// <para>
    /// The properties can be read from any thread. Each one is the value of the last step that the model made; <see cref="INotifyPropertyChanged"/>
    /// tells when a value changed, and is raised outside the model, in order (see <see cref="ObsStudioOptions.NotificationContext"/>).
    /// </para>
    /// </remarks>
    public abstract partial class ObsSource : INotifyPropertyChanged
    {
        private volatile string _name;
        private volatile ObsObjectState _state;
        private ObsFilters _filters;

        private protected ObsSource(ObsStudio studio, Guid uuid, string name, ObsObjectState state)
        {
            this.Studio = studio;
            this.Uuid = uuid;
            this._name = name;
            this._state = state;
            this._filters = new ObsFilters(this, [], studio.IgnoreCase);
        }

        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Gets the <see cref="ObsStudio"/> that this object belongs to.
        /// </summary>
        public ObsStudio Studio { get; }

        /// <summary>
        /// Gets the UUID of the object. Scenes and inputs share one namespace of UUIDs, and a UUID is never used again.
        /// </summary>
        public Guid Uuid { get; }

        /// <summary>
        /// Gets the name of the object: the current name, which changes when the object is renamed.
        /// </summary>
        public string Name => this._name;

        /// <summary>
        /// Gets what OBS Studio has done with the object: it is <see cref="ObsObjectState.Live"/>, or it was removed and is
        /// <see cref="ObsObjectState.Detached"/> (still alive for the scene items that use it), or it is <see cref="ObsObjectState.Gone"/>.
        /// </summary>
        public ObsObjectState State => this._state;

        /// <summary>
        /// Gets whether OBS Studio removed the object: <see cref="State"/> is not <see cref="ObsObjectState.Live"/>.
        /// </summary>
        public bool IsRemoved => this._state != ObsObjectState.Live;

        /// <summary>
        /// Gets the filters of the scene or input, from the first (index 0) to the last.
        /// </summary>
        /// <remarks>
        /// The collection is a snapshot: it does not change while you look at it. Ask again, or follow <see cref="PropertyChanged"/> (the name of the property is <c>Filters</c>) and
        /// <see cref="ObsStudio.Changed"/>, to see filters come and go and move. The filters of an object that was announced a moment ago can be read a moment later (see
        /// <see cref="ObsStudio.WaitUntilSettledAsync"/>).
        /// </remarks>
        public ObsFilters Filters => Volatile.Read(ref this._filters);

        internal void SetFilters(ObsFilters filters)
        {
            Volatile.Write(ref this._filters, filters);
        }

        /// <summary>
        /// Gets every scene item that shows this scene or input, in every scene (a scene that OBS Studio keeps alive after its removal included), as they are
        /// at the moment that you ask.
        /// </summary>
        /// <returns>The scene items, in the order of the list of scenes and, in each scene, from the bottom to the top.</returns>
        /// <remarks>
        /// <para>
        /// OBS Studio can show the same source in several scenes, and several times in one scene. The list is made from the scenes when you ask for it, so it
        /// takes a little time in a model with thousands of scene items, and it is not kept up to date: ask again, or follow <see cref="ObsStudio.Changed"/>.
        /// That is why this is a method, and not a property: nothing can tell you when the answer changes.
        /// </para>
        /// </remarks>
        public IReadOnlyList<ObsSceneItem> GetSceneItems()
        {
            return this.Studio.FindItemsShowing(this);
        }

        // The number of the pass of the publisher that last saw the object in the model: an object that a pass does not see is gone.
        internal int Pass { get; set; }

        // What the object is, in words, for the messages: "scene 'Gaming'", "group 'Overlays'" or "input 'Webcam'". A group is a scene of OBS Studio, and an application calls it a group.
        internal string Label => $"{(this is ObsScene { IsGroup: true } ? "group" : this is ObsScene ? "scene" : "input")} '{this.Name}'";

        internal bool SetName(string name)
        {
            if (string.Equals(this._name, name, StringComparison.Ordinal))
            {
                return false;
            }

            this._name = name;
            return true;
        }

        internal bool SetState(ObsObjectState state)
        {
            if (this._state == state)
            {
                return false;
            }

            this._state = state;
            return true;
        }

        internal void RaisePropertyChanged(string propertyName)
        {
            this.Studio.Raise(this.PropertyChanged, this, propertyName);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"{(this is ObsScene ? "Scene" : "Input")} '{this.Name}'";
        }
    }
}
