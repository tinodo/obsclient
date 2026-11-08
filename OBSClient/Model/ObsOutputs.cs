namespace OBSStudioClient.Model
{
    using System.Collections;

    /// <summary>
    /// The outputs of OBS Studio: the stream, the recording, the virtual camera and the replay buffer.
    /// </summary>
    /// <remarks>
    /// The outputs are made with the <see cref="ObsModel"/> and stay the same objects for as long as it lives: an output does not belong to a scene collection, and a connection
    /// that is lost and restored changes what they show, not which objects they are. Before the model has read them they are <see cref="ObsOutputStatus.Stopped"/>.
    /// </remarks>
    public sealed class ObsOutputs : IReadOnlyList<ObsOutput>
    {
        private readonly ObsOutput[] _all;

        internal ObsOutputs(ObsModel model)
        {
            this.Stream = new ObsOutput(model, ObsOutputKind.Stream);
            this.Record = new ObsRecordOutput(model);
            this.VirtualCamera = new ObsOutput(model, ObsOutputKind.VirtualCamera);
            this.ReplayBuffer = new ObsReplayBufferOutput(model);
            this._all = [this.Stream, this.Record, this.VirtualCamera, this.ReplayBuffer];
        }

        /// <summary>
        /// Gets the stream to the streaming service.
        /// </summary>
        public ObsOutput Stream { get; }

        /// <summary>
        /// Gets the recording, which can be paused.
        /// </summary>
        public ObsRecordOutput Record { get; }

        /// <summary>
        /// Gets the virtual camera.
        /// </summary>
        public ObsOutput VirtualCamera { get; }

        /// <summary>
        /// Gets the replay buffer, which can be saved.
        /// </summary>
        public ObsReplayBufferOutput ReplayBuffer { get; }

        /// <summary>
        /// Gets the number of outputs: four.
        /// </summary>
        public int Count => this._all.Length;

        /// <summary>
        /// Gets an output by its position: the stream, the recording, the virtual camera and the replay buffer, in that order.
        /// </summary>
        /// <param name="index">The position.</param>
        public ObsOutput this[int index] => this._all[index];

        /// <summary>
        /// Gets an output by its kind.
        /// </summary>
        /// <param name="kind">The kind.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not a kind of output.</exception>
        public ObsOutput this[ObsOutputKind kind] => (uint)kind < (uint)this._all.Length ? this._all[(int)kind] : throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a kind of output.");

        /// <inheritdoc/>
        public IEnumerator<ObsOutput> GetEnumerator()
        {
            return ((IEnumerable<ObsOutput>)this._all).GetEnumerator();
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
