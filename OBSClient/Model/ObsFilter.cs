namespace OBSStudioClient.Model
{
    using System.ComponentModel;

    /// <summary>
    /// A filter of a scene or an input of OBS Studio: something that is applied to what the source shows or plays (a color correction, a noise gate).
    /// </summary>
    /// <remarks>
    /// <para>
    /// OBS Studio gives a filter no UUID: a filter is found by its <see cref="Name"/> within its <see cref="Source"/>, and the name can change. An object stays the same object for as
    /// long as the filter exists, also when the filter is renamed or moved to another position.
    /// </para>
    /// <para>
    /// The properties can be read from any thread, and <see cref="INotifyPropertyChanged"/> tells when a value changed (see <see cref="ObsSource"/>). A reference to a filter that was removed stays
    /// valid: <see cref="IsRemoved"/> is true. The filters of a source that OBS Studio removed and keeps alive (<see cref="ObsObjectState.Detached"/>) stay in the model and can still be changed; those of a source that
    /// was destroyed (<see cref="ObsObjectState.Gone"/>) are removed with it.
    /// </para>
    /// <para>
    /// The settings of a filter are not in the model: OBS Studio does not announce a change of them, so a value that the model kept would go out of date without a sign. Ask for them with
    /// <see cref="GetSettingsAsync"/> when you need them, and change them with <see cref="SetSettingsAsync"/>.
    /// </para>
    /// </remarks>
    public sealed partial class ObsFilter : INotifyPropertyChanged
    {
        private volatile string _name;
        private int _index;
        private bool _enabled;
        private bool _removed;

        internal ObsFilter(ObsSource source, string name, string kind, int index, bool enabled)
        {
            this.Source = source;
            this._name = name;
            this.Kind = kind;
            this._index = index;
            this._enabled = enabled;
        }

        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Gets the scene or the input that the filter is on.
        /// </summary>
        public ObsSource Source { get; }

        /// <summary>
        /// Gets the name of the filter: the current name, which changes when the filter is renamed. It is unique within its source.
        /// </summary>
        public string Name => this._name;

        /// <summary>
        /// Gets the kind of the filter, as OBS Studio calls it: for example <c>color_filter_v2</c> or <c>noise_gate_filter</c>.
        /// </summary>
        public string Kind { get; }

        /// <summary>
        /// Gets the position of the filter in its source: 0 is the first, which is applied first. The positions of the filters of a source are 0, 1, 2, and so on.
        /// </summary>
        public int Index => Volatile.Read(ref this._index);

        /// <summary>
        /// Gets whether the filter is enabled. The value is current: OBS Studio announces every change of it.
        /// </summary>
        public bool IsEnabled => Volatile.Read(ref this._enabled);

        /// <summary>
        /// Gets whether the filter was removed from its source (or the source was destroyed).
        /// </summary>
        public bool IsRemoved => Volatile.Read(ref this._removed);

        // The number of the pass of the publisher that last saw the filter in the model: a filter that a pass does not see is gone.
        internal int Pass { get; set; }

        // The values are written by one thread only, the one that runs the model, and read by any.
        internal bool SetName(string name)
        {
            if (string.Equals(this._name, name, StringComparison.Ordinal))
            {
                return false;
            }

            this._name = name;
            return true;
        }

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
            this.Source.Model.Raise(this.PropertyChanged, this, propertyName);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"Filter '{this.Name}' of {this.Source.Label}";
        }
    }
}
