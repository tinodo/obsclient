namespace OBSStudioClient.Studio
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
    /// valid: <see cref="IsRemoved"/> is true.
    /// </para>
    /// <para>
    /// The settings of a filter are not a property: OBS Studio does not announce every change of them (it sends no event for most), so the model could show old values for ever.
    /// Ask OBS Studio when you need them, with the plain client: <c>obs.Client.GetSourceFilter(filter.Source.Uuid, filter.Name)</c> and <c>obs.Client.SetSourceFilterSettings(...)</c>.
    /// </para>
    /// </remarks>
    public sealed partial class ObsFilter : INotifyPropertyChanged
    {
        private volatile string _name;
        private readonly object _pendingNameLock = new();
        private string? _pendingName;
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
            this.ClearPendingName(name);
            return true;
        }

        // The name that a request has to use for the filter: the one that OBS Studio has as far as the changes that it answered go. A filter has no id, its name is what a request says, and a change
        // that has its turn right after a rename was answered, before the model shows the new name, has to say the new one. The writer sets it when it has the answer to a rename; it goes when the
        // model shows that name (or the rename is done).
        internal string NameForRequests
        {
            get
            {
                lock (this._pendingNameLock)
                {
                    return this._pendingName ?? this._name;
                }
            }
        }

        internal void SetPendingName(string name)
        {
            lock (this._pendingNameLock)
            {
                // OBS Studio makes the change, tells it, and then answers: the model can show the new name before the answer to the rename is handled, and then nothing is waiting for it. A name that
                // nothing clears (the call that made it can have ended, so that no Finish clears it) would be sent for this filter after somebody else renamed it. When an earlier name is still waiting the model
                // is behind, and a name that it shows is the one of an earlier step (the filter was renamed to a name and back, quickly), so this one is waiting too.
                if (this._pendingName is null && string.Equals(this._name, name, StringComparison.Ordinal))
                {
                    return;
                }

                this._pendingName = name;
            }
        }

        internal void ClearPendingName(string name)
        {
            lock (this._pendingNameLock)
            {
                if (string.Equals(this._pendingName, name, StringComparison.Ordinal))
                {
                    this._pendingName = null;
                }
            }
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
            this.Source.Studio.Raise(this.PropertyChanged, this, propertyName);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"Filter '{this.Name}' of {this.Source.Label}";
        }
    }
}
