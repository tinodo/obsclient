namespace OBSStudioClient.Model
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Events;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Responses;

    /// <summary>
    /// The replay buffer of OBS Studio. Besides what every output has, it can save what it holds, and it says which file it wrote.
    /// </summary>
    public sealed class ObsReplayBufferOutput : ObsOutput
    {
        private string? _lastSavedPath;

        internal ObsReplayBufferOutput(ObsModel model)
            : base(model, ObsOutputKind.ReplayBuffer)
        {
        }

        /// <summary>
        /// Occurs when OBS Studio has saved the replay buffer to a file (<see cref="SaveAsync"/>, or the hotkey). The event carries the path of the file.
        /// </summary>
        /// <remarks>
        /// It is raised in the order in which the events of OBS Studio arrived, one at a time, on <see cref="ObsModelOptions.NotificationContext"/> (or a thread of the thread pool). The same
        /// event is available as <c>Model.Client.ReplayBufferSaved</c>, which the client raises from a dispatcher of its own.
        /// </remarks>
        public event EventHandler<ReplayBufferSavedEventArgs>? Saved;

        /// <summary>
        /// Gets the path of the file that OBS Studio announced when it last saved the replay buffer, or null when the model has not seen it save one.
        /// </summary>
        /// <remarks>
        /// The model knows the replays that were saved while it was connected: OBS Studio says where it wrote the file in an event. It is kept when the connection is lost (like everything that
        /// the model holds, it is the last that was known). <see cref="GetLastReplayFileAsync"/> asks OBS Studio for the newest replay, which it knows for as long as the replay buffer runs, also
        /// when it was saved before the model connected. When the path changes, <see cref="ObsModel.Changed"/> and PropertyChanged tell, the property before the <see cref="Saved"/> event. OBS Studio
        /// writes the path with forward slashes on every platform; the model hands it on as it is.
        /// </remarks>
        public string? LastSavedPath => Volatile.Read(ref this._lastSavedPath);

        // Brings the object in line with the model. Returns whether the path changed.
        internal bool SetLastSavedPath(string? path, out string? previous)
        {
            previous = Volatile.Read(ref this._lastSavedPath);
            if (string.Equals(previous, path, StringComparison.Ordinal))
            {
                return false;
            }

            Volatile.Write(ref this._lastSavedPath, path);
            return true;
        }

        internal void RaiseSaved(ReplayBufferSavedEventArgs args)
        {
            this.Model.RaiseEvent(this.Saved, args, nameof(this.Saved), this);
        }

        /// <summary>
        /// Asks OBS Studio for the path of the newest replay that it saved.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A request that was not sent yet is not sent.</param>
        /// <returns>The path of the file. It is empty when OBS Studio has saved no replay yet.</returns>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call.</exception>
        /// <exception cref="TimeoutException">OBS Studio did not answer in time (<see cref="ObsModelOptions.WriteTimeout"/>).</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the request: the replay buffer is not running.</exception>
        /// <remarks>
        /// It is asked in its turn, after the changes that were asked for before it (see <see cref="ObsSceneItem.SetEnabledAsync"/>), so the path of a replay that was saved by a
        /// <see cref="SaveAsync"/> before it is the one that comes back, once OBS Studio has written the file.
        /// </remarks>
        public Task<string> GetLastReplayFileAsync(CancellationToken cancellationToken = default)
        {
            return this.Model.Writer.ExecuteAsync<string>(
                new WriteJob
                {
                    Description = "asking for the newest replay of the replay buffer",
                    ReadOnly = true,
                    BuildRequests = () => [new WriteRequest(RequestType.GetLastReplayBufferReplay, null)],
                    ReadResponses = static responses => ((SavedReplayPathResponse)responses[0].ResponseData!).SavedReplayPath,
                },
                cancellationToken);
        }

        /// <summary>
        /// Saves the replay buffer to a file, and returns when OBS Studio has accepted the request.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when OBS Studio has answered.</returns>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the request: the replay buffer is not running.</exception>
        /// <remarks>
        /// OBS Studio writes the file a moment after it has answered, and says where: <see cref="Saved"/> is raised, and <see cref="LastSavedPath"/> shows the path; the path of the newest replay is also
        /// what <see cref="GetLastReplayFileAsync"/> returns. Saving changes nothing else in the model, so the call does not wait for it.
        /// See <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes.
        /// </remarks>
        public Task SaveAsync(CancellationToken cancellationToken = default)
        {
            return this.Model.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = "saving the replay buffer",
                    BuildRequests = () => [new WriteRequest(RequestType.SaveReplayBuffer, null)],
                },
                cancellationToken);
        }
    }
}
