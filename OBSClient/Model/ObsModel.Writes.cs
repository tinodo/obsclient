namespace OBSStudioClient.Model
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Responses;
    using OBSStudioClient.Model.Engine;

    public sealed partial class ObsModel
    {
        /// <summary>
        /// Makes a scene, and returns it when the model shows it.
        /// </summary>
        /// <param name="name">The name of the new scene: it can not be the name of another scene. With OBS Studio that has obs-websocket 5.6 or older it can not be the name of an input either. The names are case sensitive.</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>The scene, which is in <see cref="Scenes"/> (OBS Studio lists the scene that was made last first).</returns>
        /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
        /// <exception cref="ObsNameInUseException">The name is taken (by a scene, or by an input with obs-websocket 5.6 and older). Nothing was changed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the scene.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the scene, and the model does not show it. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        /// <remarks>
        /// <para>
        /// The scene is empty. OBS Studio announces a new scene with an event and lists it a moment later (up to about two seconds), and the model shows it as soon as it is
        /// complete: the call returns then, so the scene is in <see cref="Scenes"/> when you get it. See <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes and the
        /// wait. When the user interface of OBS Studio is busy, its list can be late for much longer (13 seconds were seen on a copy of OBS Studio that had just been started). The model gives up on
        /// a scene that the list does not have after 10 seconds, and shows it again when the list has it; a call that ends before that says <see cref="ObsNotConfirmedException"/>.
        /// </para>
        /// </remarks>
        public Task<ObsScene> CreateSceneAsync(string name, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            var description = $"creating scene '{name}'";
            return this._writer.ExecuteAsync<ObsScene>(
                new WriteJob
                {
                    Description = description,
                    BuildRequests = () => [new WriteRequest(RequestType.CreateScene, new { sceneName = name })],
                    ReadResponses = static responses => ((CreateSceneResponse)responses[0].ResponseData!).SceneUuid,
                    IsConfirmed = result => this.Scenes.TryGet((Guid)result!, out _),
                    Finish = result => this.Scenes.TryGet((Guid)result!, out var scene)
                        ? scene
                        : throw new ObsNotConfirmedException(description, "the scene is not in the model: it was removed again, OBS Studio changed its scene collection, or the model was not told of it in time"),
                    Refresh = [new RefreshTarget(ReadKind.Scenes)],
                    Map = (_, response) => response.RequestStatus.Code == RequestStatusCode.ResourceAlreadyExists
                        ? ModelNames.InUse(this, description, name, forScene: true, requester: null)
                        : null,
                },
                cancellationToken);
        }
    }
}
