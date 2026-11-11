namespace OBSStudioClient.Model
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;

    // The windows of OBS Studio that belong to an input. obs-websocket opens them for inputs only: a scene has no properties dialog that it can open, and no interaction.
    public sealed partial class ObsInput
    {
        /// <summary>
        /// Opens the window with the properties of the input in OBS Studio, and returns when OBS Studio has answered.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when OBS Studio has opened the window. It does not wait for the window to be closed again.</returns>
        /// <exception cref="ObsRemovedException">The input was destroyed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it opened the window.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the request.</exception>
        /// <remarks>
        /// The window opens on the computer that OBS Studio runs on, for the person at that computer. What is changed in it reaches the model through the events, like any other change. The
        /// call changes nothing in the model, so it does not wait for it. See <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes.
        /// </remarks>
        public Task OpenPropertiesDialogAsync(CancellationToken cancellationToken = default)
        {
            return this.OpenDialogAsync(RequestType.OpenInputPropertiesDialog, "the properties window", cancellationToken);
        }

        /// <summary>
        /// Opens the window with the filters of the input in OBS Studio, and returns when OBS Studio has answered.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when OBS Studio has opened the window. It does not wait for the window to be closed again.</returns>
        /// <exception cref="ObsRemovedException">The input was destroyed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it opened the window.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the request.</exception>
        /// <remarks>
        /// obs-websocket opens this window for inputs only, so a scene has no such method. See <see cref="OpenPropertiesDialogAsync"/> for what to know about the windows of OBS Studio.
        /// </remarks>
        public Task OpenFiltersDialogAsync(CancellationToken cancellationToken = default)
        {
            return this.OpenDialogAsync(RequestType.OpenInputFiltersDialog, "the filters window", cancellationToken);
        }

        /// <summary>
        /// Opens the window in which a person can use the input as if it was on the screen (a browser source: click, type, scroll), and returns when OBS Studio has answered.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when OBS Studio has opened the window. It does not wait for the window to be closed again.</returns>
        /// <exception cref="ObsRemovedException">The input was destroyed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it opened the window.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the request: the input does not support interaction (<see cref="ObsResponseException.ErrorCode"/> is <see cref="RequestStatusCode.InvalidResourceState"/>).</exception>
        /// <remarks>See <see cref="OpenPropertiesDialogAsync"/> for what to know about the windows of OBS Studio.</remarks>
        public Task OpenInteractDialogAsync(CancellationToken cancellationToken = default)
        {
            return this.OpenDialogAsync(RequestType.OpenInputInteractDialog, "the interaction window", cancellationToken);
        }

        private Task OpenDialogAsync(RequestType request, string window, CancellationToken cancellationToken)
        {
            return this.Model.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = $"opening {window} of {this.Label}",
                    Subject = this.Label,
                    Check = this.CheckNotDestroyed,
                    BuildRequests = () => [new WriteRequest(request, new { inputUuid = this.Uuid })],
                },
                cancellationToken);
        }
    }
}
