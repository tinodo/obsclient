namespace OBSStudioClient.Model
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;

    /// <summary>
    /// The replay buffer of OBS Studio. Besides what every output has, it can save what it holds.
    /// </summary>
    public sealed class ObsReplayBufferOutput : ObsOutput
    {
        internal ObsReplayBufferOutput(ObsModel model)
            : base(model, ObsOutputKind.ReplayBuffer)
        {
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
        /// OBS Studio writes the file a moment after it has answered, and says where in the <c>ReplayBufferSaved</c> event (<c>obs.Client.ReplayBufferSaved</c>); the path of the newest
        /// replay is also what <c>obs.Client.GetLastReplayBufferReplay()</c> returns. Saving changes nothing in the model, so the call does not wait for it.
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
