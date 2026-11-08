namespace OBSStudioClient.Studio
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Studio.Engine;

    /// <summary>
    /// The recording of OBS Studio. Besides what every output has, it can be paused.
    /// </summary>
    public sealed class ObsRecordOutput : ObsOutput
    {
        internal ObsRecordOutput(ObsStudio studio)
            : base(studio, ObsOutputKind.Record)
        {
        }

        /// <summary>
        /// Gets whether the recording is paused: <see cref="ObsOutput.Status"/> is <see cref="ObsOutputStatus.Paused"/>.
        /// </summary>
        public bool IsPaused => this.Status == ObsOutputStatus.Paused;

        /// <summary>
        /// Pauses the recording, and returns when the model shows it.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when <see cref="IsPaused"/> is true, or when the model has been read again after the events did not show it.</returns>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the change, and the model does not show it. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change: the recording is not running, or is paused already.</exception>
        /// <remarks>See <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes and the wait.</remarks>
        public Task PauseAsync(CancellationToken cancellationToken = default)
        {
            return this.Studio.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = "pausing the recording",
                    BuildRequests = () => [new WriteRequest(RequestType.PauseRecord, null)],
                    IsConfirmed = _ => this.IsPaused,
                    Refresh = [new RefreshTarget(ReadKind.Record)],
                },
                cancellationToken);
        }

        /// <summary>
        /// Resumes the recording after it was paused, and returns when the model shows it.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when <see cref="IsPaused"/> is false, or when the model has been read again after the events did not show it.</returns>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the change, and the model does not show it. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change: the recording is not paused.</exception>
        /// <remarks>See <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes and the wait.</remarks>
        public Task ResumeAsync(CancellationToken cancellationToken = default)
        {
            return this.Studio.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = "resuming the recording",
                    BuildRequests = () => [new WriteRequest(RequestType.ResumeRecord, null)],
                    IsConfirmed = _ => !this.IsPaused,
                    Refresh = [new RefreshTarget(ReadKind.Record)],
                },
                cancellationToken);
        }
    }
}
