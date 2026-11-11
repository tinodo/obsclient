namespace OBSStudioClient.Model
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Messages;
    using OBSStudioClient.Model.Engine;
    using OBSStudioClient.Responses;

    // Where a scene item is, how it is shown (its transform and its blend mode), and a copy of it.
    public sealed partial class ObsSceneItem
    {
        /// <summary>
        /// Asks OBS Studio for the transform of the scene item: where it is, how large it is shown, how it is rotated, cropped and bounded.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A request that was not sent yet is not sent.</param>
        /// <returns>The transform at the moment that OBS Studio answered. It can be turned into a change with <see cref="SceneItemTransform.ToUpdate"/>.</returns>
        /// <exception cref="ObsRemovedException">The scene item, or its scene, was removed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call.</exception>
        /// <exception cref="TimeoutException">OBS Studio did not answer in time (<see cref="ObsModelOptions.WriteTimeout"/>).</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the request.</exception>
        /// <remarks>
        /// <see cref="Transform"/> is the same, as the model holds it, when the model was asked to follow the transforms (<see cref="ObsHighVolume.Transforms"/>). The request is made in its turn, after the
        /// changes that were asked for before it (see <see cref="SetEnabledAsync"/>).
        /// </remarks>
        public Task<SceneItemTransform> GetTransformAsync(CancellationToken cancellationToken = default)
        {
            return this.Scene.Model.Writer.ExecuteAsync<SceneItemTransform>(
                new WriteJob
                {
                    Description = $"asking for the transform of {this.Subject()}",
                    Subject = this.Subject(),
                    SubjectGone = () => this.IsRemoved,
                    ReadOnly = true,
                    Check = this.CheckWritable,
                    BuildRequests = () => [new WriteRequest(RequestType.GetSceneItemTransform, new { sceneUuid = this.Scene.Uuid, sceneItemId = this.Id })],
                    ReadResponses = static responses => ((SceneItemTransformResponse)responses[0].ResponseData!).SceneItemTransform,
                },
                cancellationToken);
        }

        /// <summary>
        /// Changes the transform of the scene item (its position, scale, rotation, alignment, bounds and crop), and returns when OBS Studio has made the change, and when the model shows it if it follows the transforms.
        /// </summary>
        /// <param name="update">What to change: only the members that are set are sent, and OBS Studio leaves every other member as it is. Start from <see cref="SceneItemTransform.ToUpdate"/> to change what is there.</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when <see cref="Transform"/> shows the change (when the model follows the transforms), or when OBS Studio has answered (when it does not).</returns>
        /// <exception cref="ArgumentNullException"><paramref name="update"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="update"/> sets no member (<see cref="SceneItemTransformUpdate.IsEmpty"/>): OBS Studio refuses that.</exception>
        /// <exception cref="ObsRemovedException">The scene item, or its scene, was removed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the change, and the model does not show it: the events did not show it in time, and the model could not be read again in time. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change: a rotation above 360 degrees, a crop below 0, or a bounds width or height below 1 (the bounds of a transform without bounds are left out of <see cref="SceneItemTransform.ToUpdate"/> for that reason).</exception>
        /// <remarks>
        /// <para>
        /// OBS Studio makes the change in steps that it announces one by one (a change of the crop together with another comes as two events), and the call waits until <see cref="Transform"/> has all of it. When the
        /// model does not follow the transforms there is nothing of it to wait for: the call returns when OBS Studio has answered. The change itself is one request, which OBS Studio makes as a whole.
        /// </para>
        /// <para>
        /// OBS Studio 33.0.0 announces a crop only for a scene that it renders (the program scene, the preview scene of studio mode, a scene that a projector shows): for another scene it announces the crop when the
        /// scene is rendered. A model that follows the transforms therefore reads the scene again after a change that sets a crop, instead of waiting for an announcement that may not come.
        /// </para>
        /// <para>
        /// See <see cref="SetEnabledAsync"/> for the order of changes and the wait.
        /// </para>
        /// </remarks>
        public Task SetTransformAsync(SceneItemTransformUpdate update, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(update);
            if (update.IsEmpty)
            {
                throw new ArgumentException("The update sets no member: OBS Studio refuses a change of nothing. Set at least one member.", nameof(update));
            }

            var model = this.Scene.Model;
            var follows = model.HighVolume.HasFlag(ObsHighVolume.Transforms);
            var setsCrop = update.CropBottom is not null || update.CropLeft is not null || update.CropRight is not null || update.CropTop is not null;
            return model.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = $"changing the transform of {this.Subject()}",
                    Subject = this.Subject(),
                    SubjectGone = () => this.IsRemoved,
                    Check = this.CheckWritable,
                    BuildRequests = () => [new WriteRequest(RequestType.SetSceneItemTransform, new { sceneUuid = this.Scene.Uuid, sceneItemId = this.Id, sceneItemTransform = update })],
                    IsConfirmed = _ => this.IsRemoved || !follows || ShowsTheChange(this.Transform, update),
                    Refresh = follows ? [new RefreshTarget(ReadKind.Items, this.Scene.Uuid)] : [],
                    Observe = follows && setsCrop ? [new RefreshTarget(ReadKind.Items, this.Scene.Uuid)] : [],
                },
                cancellationToken);
        }

        /// <summary>
        /// Asks OBS Studio how the scene item is blended with what is behind it.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A request that was not sent yet is not sent.</param>
        /// <returns>The blend mode at the moment that OBS Studio answered.</returns>
        /// <exception cref="ObsRemovedException">The scene item, or its scene, was removed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call.</exception>
        /// <exception cref="TimeoutException">OBS Studio did not answer in time (<see cref="ObsModelOptions.WriteTimeout"/>).</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the request.</exception>
        /// <remarks>
        /// OBS Studio does not announce a change of the blend mode, so the model does not keep it: ask when you need it. (A change of it makes OBS Studio send the transform of the item again, which is no change of the
        /// transform: <see cref="Transform"/> does not change.) See <see cref="SetEnabledAsync"/> for the order of changes.
        /// </remarks>
        public Task<BlendMode> GetBlendModeAsync(CancellationToken cancellationToken = default)
        {
            return this.Scene.Model.Writer.ExecuteAsync<BlendMode>(
                new WriteJob
                {
                    Description = $"asking for the blend mode of {this.Subject()}",
                    Subject = this.Subject(),
                    SubjectGone = () => this.IsRemoved,
                    ReadOnly = true,
                    Check = this.CheckWritable,
                    BuildRequests = () => [new WriteRequest(RequestType.GetSceneItemBlendMode, new { sceneUuid = this.Scene.Uuid, sceneItemId = this.Id })],
                    ReadResponses = static responses => ((SceneItemBlendModeResponse)responses[0].ResponseData!).SceneItemBlendMode,
                },
                cancellationToken);
        }

        /// <summary>
        /// Sets how the scene item is blended with what is behind it, and returns when OBS Studio has made the change.
        /// </summary>
        /// <param name="mode">The blend mode.</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when OBS Studio has answered.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is not a blend mode.</exception>
        /// <exception cref="ObsRemovedException">The scene item, or its scene, was removed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        /// <remarks>
        /// The model does not keep the blend mode (OBS Studio does not announce a change of it), so the call does not wait for the model: it returns when OBS Studio has answered. See
        /// <see cref="SetEnabledAsync"/> for the order of changes.
        /// </remarks>
        public Task SetBlendModeAsync(BlendMode mode, CancellationToken cancellationToken = default)
        {
            if (!Enum.IsDefined(mode))
            {
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "That is not a blend mode.");
            }

            return this.Scene.Model.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = $"setting the blend mode of {this.Subject()} to {mode}",
                    Subject = this.Subject(),
                    SubjectGone = () => this.IsRemoved,
                    Check = this.CheckWritable,
                    BuildRequests = () => [new WriteRequest(RequestType.SetSceneItemBlendMode, new { sceneUuid = this.Scene.Uuid, sceneItemId = this.Id, sceneItemBlendMode = mode.ToString() })],
                },
                cancellationToken);
        }

        /// <summary>
        /// Makes a copy of the scene item, on top of the scene that the copy is made in, and returns the copy when the model shows it.
        /// </summary>
        /// <param name="destination">The scene to make the copy in, or null for the scene that the scene item is in.</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>The copy, which is in <see cref="ObsScene.Items"/> of the scene that it was made in.</returns>
        /// <exception cref="ArgumentException"><paramref name="destination"/> belongs to another <see cref="ObsModel"/>.</exception>
        /// <exception cref="ObsRemovedException">The scene item, its scene or the destination was removed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the copy.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the copy, and the model does not show it. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        /// <remarks>
        /// <para>
        /// What OBS Studio 33.0.0 copies is the transform and the crop, and whether the scene item is shown; it does not copy whether it is locked, nor the blend mode (the copy is unlocked, and has the normal blend
        /// mode). It shows the same source: a copy is a scene item of its own, with an id of its own, and it is not another input.
        /// </para>
        /// <para>
        /// OBS Studio 33.0.0 (obs-websocket 5.7.6) makes the copy in the scene of the original when the destination is given by its UUID alone: it reads the destination only when the request has its name. The model gives both
        /// (the UUID that is there is the one that finds the scene), and so does not depend on it. The plain client does: <see cref="ObsClient.DuplicateSceneItem"/> with a destination that is a UUID makes the copy in the
        /// scene of the original. See <see cref="SetEnabledAsync"/> for the order of changes and the wait.
        /// </para>
        /// </remarks>
        public Task<ObsSceneItem> DuplicateAsync(ObsScene? destination = null, CancellationToken cancellationToken = default)
        {
            if (destination is not null && !ReferenceEquals(destination.Model, this.Scene.Model))
            {
                throw new ArgumentException("The scene belongs to another ObsModel.", nameof(destination));
            }

            var target = destination ?? this.Scene;
            var follows = this.Scene.Model.HighVolume.HasFlag(ObsHighVolume.Transforms);
            var description = $"duplicating {this.Subject()} into scene '{target.Name}'";

            // OBS Studio announces a copy in steps, and the crop is the last of them (and only for a scene that it renders): the copy is shown when the model has the crop of the original, which the model only keeps
            // when it follows the transforms, and that model reads the scene after the answer for it.
            SceneItemTransform? original = null;
            return this.Scene.Model.Writer.ExecuteAsync<ObsSceneItem>(
                new WriteJob
                {
                    Description = description,
                    Subject = this.Subject(),
                    SubjectGone = () => this.IsRemoved || target.IsRemoved,
                    Check = () =>
                    {
                        this.CheckWritable();
                        if (target.State != ObsObjectState.Live)
                        {
                            throw new ObsRemovedException(target.Label, target.State);
                        }
                    },
                    BuildRequests = () =>
                    {
                        original = this.Transform;
                        return [new WriteRequest(RequestType.DuplicateSceneItem, this.DuplicateData(destination))];
                    },
                    ReadResponses = static responses => ((SceneItemIdResponse)responses[0].ResponseData!).SceneItemId,
                    IsConfirmed = result => this.IsRemoved || target.IsRemoved || (target.FindItem((int)result!) is { } copy && HasTheCropOf(copy.Transform, original)),
                    Finish = result => target.FindItem((int)result!) ?? throw (target.State != ObsObjectState.Live
                        ? new ObsRemovedException(target.Label, target.State)
                        : new ObsNotConfirmedException(description, "the scene item is not in the model: it was removed again, OBS Studio changed its scene collection, or the model was not told of it in time")),
                    Refresh = [new RefreshTarget(ReadKind.Items, target.Uuid)],
                    Observe = follows ? [new RefreshTarget(ReadKind.Items, target.Uuid)] : [],

                    // OBS Studio also says "not found" for the destination, which is not the scene item that this call is about.
                    Map = (_, response) => response.RequestStatus.Code == RequestStatusCode.ResourceNotFound && destination is not null && MentionsDestination(response.RequestStatus.Comment, destination)
                        ? new ObsRemovedException(destination.Label, ObsObjectState.Gone)
                        : null,
                },
                cancellationToken);
        }

        // What is sent: the destination is left out for a copy in the scene of the original, which is what OBS Studio makes when it is not given. A destination is given by its UUID and by its name: obs-websocket
        // 5.7.6 only reads the destination when the request has its name (a request with the UUID alone makes the copy in the scene of the original), and then the UUID that is there is the one that finds the scene.
        private object DuplicateData(ObsScene? destination)
        {
            return destination is null
                ? new { sceneUuid = this.Scene.Uuid, sceneItemId = this.Id }
                : new { sceneUuid = this.Scene.Uuid, sceneItemId = this.Id, destinationSceneName = destination.Name, destinationSceneUuid = destination.Uuid };
        }

        private static bool MentionsDestination(string? comment, ObsScene destination)
        {
            return comment is not null
                && (comment.Contains(destination.Uuid.ToString("D"), StringComparison.OrdinalIgnoreCase) || comment.Contains(destination.Name, StringComparison.Ordinal));
        }

        // Whether the transform of a copy has the crop that the original had: nothing is asked of a model that does not follow the transforms.
        private static bool HasTheCropOf(SceneItemTransform? copy, SceneItemTransform? original)
        {
            return original is null
                || (copy is not null && copy.CropLeft == original.CropLeft && copy.CropRight == original.CropRight && copy.CropTop == original.CropTop && copy.CropBottom == original.CropBottom);
        }

        // Whether the transform that the model shows is what a change asked for: every member that the change sets. OBS Studio keeps a number as a float, so the numbers are compared as such.
        private static bool ShowsTheChange(SceneItemTransform? shown, SceneItemTransformUpdate update)
        {
            return shown is not null
                && (update.Alignment is not { } alignment || shown.Alignment == alignment)
                && (update.BoundsAlignment is not { } boundsAlignment || shown.BoundsAlignment == boundsAlignment)
                && (update.BoundsHeight is not { } boundsHeight || Near(shown.BoundsHeight, boundsHeight))
                && (update.BoundsType is not { } boundsType || shown.BoundsType == boundsType)
                && (update.BoundsWidth is not { } boundsWidth || Near(shown.BoundsWidth, boundsWidth))
                && (update.CropBottom is not { } cropBottom || shown.CropBottom == cropBottom)
                && (update.CropLeft is not { } cropLeft || shown.CropLeft == cropLeft)
                && (update.CropRight is not { } cropRight || shown.CropRight == cropRight)
                && (update.CropTop is not { } cropTop || shown.CropTop == cropTop)

                // An OBS Studio that does not say whether the item is cropped to its bounds cannot show that it is.
                && (update.CropToBounds is not { } cropToBounds || shown.CropToBounds is null || shown.CropToBounds == cropToBounds)
                && (update.PositionX is not { } positionX || Near(shown.PositionX, positionX))
                && (update.PositionY is not { } positionY || Near(shown.PositionY, positionY))
                && (update.Rotation is not { } rotation || Near(shown.Rotation, rotation))
                && (update.ScaleX is not { } scaleX || Near(shown.ScaleX, scaleX))
                && (update.ScaleY is not { } scaleY || Near(shown.ScaleY, scaleY));
        }

        private static bool Near(float shown, float asked)
        {
            return MathF.Abs(shown - asked) <= 0.0001f * MathF.Max(1f, MathF.Abs(asked));
        }
    }
}
