namespace OBSStudioClient.Model
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Messages;
    using OBSStudioClient.Model.Engine;

    // Studio mode: switching it on and off. The preview scene and the transitions belong to it: ObsModel.PreviewScene, ObsScene.SwitchToPreviewAsync and ObsModel.Transitions.
    public sealed partial class ObsModel
    {
        /// <summary>
        /// Switches studio mode on or off, and returns when the model shows it.
        /// </summary>
        /// <param name="enabled">True to switch studio mode on, false to switch it off.</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when <see cref="IsStudioModeEnabled"/> is <paramref name="enabled"/>.</returns>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the change, and the model does not show it: the events did not show it in time, and the model could not be read again in time. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        /// <remarks>
        /// <para>
        /// With studio mode on there is a preview scene (<see cref="PreviewScene"/>) next to the program scene: OBS Studio makes it the program scene (the scene that is on the air now) when
        /// studio mode is switched on, and the model reads it, so <see cref="PreviewScene"/> follows a moment after <see cref="IsStudioModeEnabled"/>. OBS Studio sends no event when studio mode is on already.
        /// </para>
        /// <para>
        /// Do not remove the program scene while studio mode is on: OBS Studio 33.0.0 then stopped answering the requests that need its window for seconds (up to half a minute was seen), and the model
        /// reads again until it does. Switch to another scene first. See <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes and the wait.
        /// </para>
        /// </remarks>
        public Task SetStudioModeEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
        {
            return this._writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = enabled ? "switching studio mode on" : "switching studio mode off",
                    BuildRequests = () => [new WriteRequest(RequestType.SetStudioModeEnabled, new { studioModeEnabled = enabled })],
                    IsConfirmed = _ => this.IsStudioModeEnabled == enabled,
                    Refresh = [new RefreshTarget(ReadKind.StudioMode)],
                },
                cancellationToken);
        }
    }
}
