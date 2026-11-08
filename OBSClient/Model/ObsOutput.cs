namespace OBSStudioClient.Model
{
    using System.ComponentModel;

    /// <summary>
    /// An output of OBS Studio: the stream, the recording, the virtual camera or the replay buffer. It is the same object for as long as the <see cref="ObsModel"/> lives.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <see cref="Status"/> is kept up to date with the events of OBS Studio, and is read again when the connection is restored, so <see cref="IsActive"/> answers at once,
    /// from any thread, without a request. An output does not belong to a scene collection: switching the scene collection does not change it, and the objects are not replaced.
    /// </para>
    /// <para>
    /// <see cref="StartAsync"/>, <see cref="StopAsync"/> and <see cref="ToggleAsync"/> are changes like the others (see <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of
    /// changes and the wait).
    /// </para>
    /// </remarks>
    public partial class ObsOutput : INotifyPropertyChanged
    {
        private int _status;
        private int _starts;
        private int _stops;

        internal ObsOutput(ObsModel model, ObsOutputKind kind)
        {
            this.Model = model;
            this.Kind = kind;
        }

        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Gets the model that the output belongs to.
        /// </summary>
        public ObsModel Model { get; }

        /// <summary>
        /// Gets which output it is.
        /// </summary>
        public ObsOutputKind Kind { get; }

        /// <summary>
        /// Gets the name of the output, as the messages call it: "stream", "recording", "virtual camera" or "replay buffer".
        /// </summary>
        public string Name => this.Kind switch
        {
            ObsOutputKind.Stream => "stream",
            ObsOutputKind.Record => "recording",
            ObsOutputKind.VirtualCamera => "virtual camera",
            _ => "replay buffer",
        };

        /// <summary>
        /// Gets what the output is doing. It is <see cref="ObsOutputStatus.Stopped"/> until the model has read it.
        /// </summary>
        public ObsOutputStatus Status => (ObsOutputStatus)Volatile.Read(ref this._status);

        /// <summary>
        /// Gets whether the output is running: <see cref="Status"/> is <see cref="ObsOutputStatus.Started"/>, <see cref="ObsOutputStatus.Reconnecting"/> or <see cref="ObsOutputStatus.Paused"/>.
        /// An output that is starting or stopping is not.
        /// </summary>
        public bool IsActive => this.Status is ObsOutputStatus.Started or ObsOutputStatus.Reconnecting or ObsOutputStatus.Paused;

        // The number of events that said that the output started (or is starting), and that it stopped (or is stopping). A change that starts or stops the output is done when the
        // model has seen such an event after the change was asked for, also when the output has gone through the step and on to another one within a single step of the model.
        internal int StartsSeen => Volatile.Read(ref this._starts);

        internal int StopsSeen => Volatile.Read(ref this._stops);

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"The {this.Name}: {this.Status}";
        }

        // Brings the object in line with the model. Returns whether the status changed.
        internal bool Publish(ObsOutputStatus status, int starts, int stops, out ObsOutputStatus previous)
        {
            Volatile.Write(ref this._starts, starts);
            Volatile.Write(ref this._stops, stops);
            previous = this.Status;
            if (previous == status)
            {
                return false;
            }

            Volatile.Write(ref this._status, (int)status);
            return true;
        }

        internal void RaisePropertyChanged(string propertyName)
        {
            this.Model.Raise(this.PropertyChanged, this, propertyName);
        }
    }
}
