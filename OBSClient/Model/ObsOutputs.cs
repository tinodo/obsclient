namespace OBSStudioClient.Model
{
    using System.Collections;
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Responses;

    /// <summary>
    /// The outputs of OBS Studio: the stream, the recording, the virtual camera and the replay buffer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The outputs are made with the <see cref="ObsModel"/> and stay the same objects for as long as it lives: an output does not belong to a scene collection, and a connection
    /// that is lost and restored changes what they show, not which objects they are. Before the model has read them they are <see cref="ObsOutputStatus.Stopped"/>.
    /// </para>
    /// <para>
    /// OBS Studio can have other outputs, which plugins add: <see cref="GetAllAsync"/> lists them, and <see cref="Named"/> gives one to start, stop and ask about. This collection is the four
    /// that the model keeps current.
    /// </para>
    /// </remarks>
    public sealed class ObsOutputs : IReadOnlyList<ObsOutput>
    {
        private readonly ObsModel _model;
        private readonly ObsOutput[] _all;

        internal ObsOutputs(ObsModel model)
        {
            this._model = model;
            this.Stream = new ObsStreamOutput(model);
            this.Record = new ObsRecordOutput(model);
            this.VirtualCamera = new ObsOutput(model, ObsOutputKind.VirtualCamera);
            this.ReplayBuffer = new ObsReplayBufferOutput(model);
            this._all = [this.Stream, this.Record, this.VirtualCamera, this.ReplayBuffer];
        }

        /// <summary>
        /// Gets the stream to the streaming service, which can send captions.
        /// </summary>
        public ObsStreamOutput Stream { get; }

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

        /// <summary>
        /// Asks OBS Studio for every output that it has: its own, and those that plugins add.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A request that was not sent yet is not sent.</param>
        /// <returns>
        /// The outputs, each with the name that <see cref="Named"/> takes, its kind (<c>rtmp_output</c>, <c>virtualcam_output</c>...), whether it runs, its size and its flags (what it can do: video, audio,
        /// encoded...).
        /// </returns>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call.</exception>
        /// <exception cref="TimeoutException">OBS Studio did not answer in time (<see cref="ObsModelOptions.WriteTimeout"/>).</exception>
        /// <remarks>
        /// The list holds the four outputs of this collection too, under the names that OBS Studio gave them (they differ between its simple and advanced output mode), and OBS Studio does not say
        /// which of the outputs of the list is which: it says the kind of an output, and a plugin can make outputs of the same kind. The answer is a snapshot of the moment that OBS Studio answered.
        /// </remarks>
        public Task<IReadOnlyList<Output>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return this._model.Writer.ExecuteAsync<IReadOnlyList<Output>>(
                new WriteJob
                {
                    Description = "asking for the outputs of OBS Studio",
                    ReadOnly = true,
                    BuildRequests = () => [new WriteRequest(RequestType.GetOutputList, null)],
                    ReadResponses = static responses => ((OutputsResponse)responses[0].ResponseData!).Outputs,
                },
                cancellationToken);
        }

        /// <summary>
        /// Gives an output of OBS Studio by its name, to start, stop and ask about: one that a plugin added, or one of the four by the name that OBS Studio gave it.
        /// </summary>
        /// <param name="name">The name of the output, as <see cref="GetAllAsync"/> lists it. It is case sensitive.</param>
        /// <returns>An object that does the calls. It does not check that the output exists, and holds nothing of it: it can be kept, and used again.</returns>
        /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
        public ObsNamedOutput Named(string name)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            return new ObsNamedOutput(this._model, name);
        }

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
