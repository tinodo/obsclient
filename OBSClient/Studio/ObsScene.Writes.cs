namespace OBSStudioClient.Studio
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Studio.Engine;

    public sealed partial class ObsScene
    {
        /// <summary>
        /// Makes the scene the program scene (the one that is on the air), and returns when the model shows it.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when <see cref="IsProgram"/> is true, or when the model has been read again after the events did not show it.</returns>
        /// <exception cref="InvalidOperationException">The scene is a group (<see cref="IsGroup"/>): a group is not a scene that can be on the air.</exception>
        /// <exception cref="ObsRemovedException">The scene was removed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the change, and the model does not show it: the events did not show it in time, and the model could not be read again in time. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        /// <remarks>
        /// <para>
        /// OBS Studio shows the scene through the transition that is set, and announces the new program scene when the transition has ended, which can take longer than
        /// <see cref="ObsStudioOptions.ConfirmationTimeout"/>. It answers a question about the program scene with the new scene at once, so the model reads it, and the task
        /// completes while the transition is still running. See <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes and the wait.
        /// </para>
        /// </remarks>
        public Task SwitchToAsync(CancellationToken cancellationToken = default)
        {
            return this.Studio.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = $"switching to scene '{this.Name}'",
                    Subject = $"scene '{this.Name}'",
                    Check = this.CheckCanBeProgram,
                    BuildRequests = () => [new WriteRequest(RequestType.SetCurrentProgramScene, new { sceneUuid = this.Uuid })],
                    IsConfirmed = _ => this.IsRemoved || this.IsProgram,
                    Refresh = [new RefreshTarget(ReadKind.Program)],
                },
                cancellationToken);
        }

        private void CheckCanBeProgram()
        {
            if (this.State != ObsObjectState.Live)
            {
                throw new ObsRemovedException($"scene '{this.Name}'", this.State);
            }

            if (this.IsGroup)
            {
                throw new InvalidOperationException($"The group '{this.Name}' can not be the program scene.");
            }
        }
    }
}
