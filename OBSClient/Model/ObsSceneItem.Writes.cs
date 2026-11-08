namespace OBSStudioClient.Model
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Responses;
    using OBSStudioClient.Model.Engine;

    public sealed partial class ObsSceneItem
    {
        /// <summary>
        /// Shows or hides the scene item, and returns when the model shows it.
        /// </summary>
        /// <param name="value">True to show the item, false to hide it.</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when <see cref="IsEnabled"/> is <paramref name="value"/>, or when the model has been read again after the events did not show it.</returns>
        /// <exception cref="ObsRemovedException">The scene item, or its scene, was removed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio, or the connection that the change was asked for on has ended and another was made. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the change, and the model does not show it: the events did not show it in time, and the model could not be read again in time. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        /// <remarks>
        /// <para>
        /// The changes that you ask for are made one at a time, in the order in which you asked for them. OBS Studio announces a change with an event, which can come a few
        /// milliseconds after its answer: the task completes when <see cref="IsEnabled"/> says what you asked for, so that what you read next is what you changed. When you ask
        /// for several changes of the same scene one after the other without waiting for each, the model shows them one after the other, and each task completes when the model
        /// shows its change after the changes before it (not when it shows the same value from before them). When the
        /// event does not come within <see cref="ObsModelOptions.ConfirmationTimeout"/>, the model reads the scene items of the scene, and the task completes with what
        /// OBS Studio holds then (somebody else may have changed it again). When OBS Studio changes its scene collection before the model shows the change, the task completes
        /// too: the change was made in the collection that was left, and this object is <see cref="ObsObjectState.Gone"/> by then. When the model can not be read again
        /// in time either, the task ends with an <see cref="ObsNotConfirmedException"/>: OBS Studio made the change, and the model does not show it yet.
        /// </para>
        /// <para>
        /// A token that ends before the request is sent keeps it from being sent. After that the change is not undone: OBS Studio may still make it, and the model follows it
        /// through the events. The next change waits until OBS Studio has answered this one (at most <see cref="ObsModelOptions.WriteTimeout"/>), whether the token ended or
        /// not, so that the changes are made in the order in which you asked for them.
        /// </para>
        /// </remarks>
        public Task SetEnabledAsync(bool value, CancellationToken cancellationToken = default)
        {
            return this.Scene.Model.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = $"{(value ? "showing" : "hiding")} {this.Subject()}",
                    Subject = this.Subject(),
                    SubjectGone = () => this.IsRemoved,
                    Check = this.CheckWritable,
                    BuildRequests = () => [new WriteRequest(RequestType.SetSceneItemEnabled, new { sceneUuid = this.Scene.Uuid, sceneItemId = this.Id, sceneItemEnabled = value })],
                    IsConfirmed = _ => this.IsRemoved || this.IsEnabled == value,
                    Refresh = [new RefreshTarget(ReadKind.Items, this.Scene.Uuid)],
                },
                cancellationToken);
        }

        /// <summary>
        /// Shows the scene item when it is hidden, and hides it when it is shown, and returns when the model shows it.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes with the value that <see cref="IsEnabled"/> was set to.</returns>
        /// <exception cref="ObsRemovedException">The scene item, or its scene, was removed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call, or did not say in time whether the item is shown. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the change, and the model does not show it: the events did not show it in time, and the model could not be read again in time. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        /// <remarks>
        /// <para>
        /// OBS Studio has no request that toggles a scene item. When its turn has come, after every change that you asked for before it has been answered, the change asks
        /// OBS Studio whether the item is shown, and sets the opposite. So two calls make two toggles, in the order in which you asked for them, and a change that you asked
        /// for between them is not overtaken: what a toggle turns over is what OBS Studio holds at its turn, not what the model showed when you called (the model can be a
        /// few milliseconds behind OBS Studio, and does not know yet what a change that was just answered did).
        /// </para>
        /// <para>
        /// The question changes nothing: when OBS Studio does not answer it in time, or the connection ends meanwhile, nothing was changed. See <see cref="SetEnabledAsync"/>
        /// for the order of changes and the wait.
        /// </para>
        /// </remarks>
        public Task<bool> ToggleEnabledAsync(CancellationToken cancellationToken = default)
        {
            // What OBS Studio holds when the turn of this change has come.
            var enabled = false;
            return this.Scene.Model.Writer.ExecuteAsync<bool>(
                new WriteJob
                {
                    Description = $"toggling {this.Subject()}",
                    Subject = this.Subject(),
                    SubjectGone = () => this.IsRemoved,
                    Check = this.CheckWritable,
                    Prepare = async ask =>
                    {
                        var answer = await ask(new WriteRequest(RequestType.GetSceneItemEnabled, new { sceneUuid = this.Scene.Uuid, sceneItemId = this.Id })).ConfigureAwait(false);
                        enabled = ((SceneItemEnabledResponse)answer.ResponseData!).SceneItemEnabled;
                    },
                    BuildRequests = () => [new WriteRequest(RequestType.SetSceneItemEnabled, new { sceneUuid = this.Scene.Uuid, sceneItemId = this.Id, sceneItemEnabled = !enabled })],
                    ReadResponses = _ => !enabled,
                    IsConfirmed = result => this.IsRemoved || this.IsEnabled == (bool)result!,
                    Refresh = [new RefreshTarget(ReadKind.Items, this.Scene.Uuid)],
                },
                cancellationToken);
        }

        /// <summary>
        /// Locks or unlocks the scene item, and returns when the model shows it.
        /// </summary>
        /// <param name="value">True to lock the item, false to unlock it.</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when <see cref="IsLocked"/> is <paramref name="value"/>.</returns>
        /// <exception cref="ObsRemovedException">The scene item, or its scene, was removed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the change, and the model does not show it: the events did not show it in time, and the model could not be read again in time. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        /// <remarks>See <see cref="SetEnabledAsync"/> for the order of changes and the wait.</remarks>
        public Task SetLockedAsync(bool value, CancellationToken cancellationToken = default)
        {
            return this.Scene.Model.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = $"{(value ? "locking" : "unlocking")} {this.Subject()}",
                    Subject = this.Subject(),
                    SubjectGone = () => this.IsRemoved,
                    Check = this.CheckWritable,
                    BuildRequests = () => [new WriteRequest(RequestType.SetSceneItemLocked, new { sceneUuid = this.Scene.Uuid, sceneItemId = this.Id, sceneItemLocked = value })],
                    IsConfirmed = _ => this.IsRemoved || this.IsLocked == value,
                    Refresh = [new RefreshTarget(ReadKind.Items, this.Scene.Uuid)],
                },
                cancellationToken);
        }

        /// <summary>
        /// Moves the scene item to a position in its scene, and returns when the model shows it.
        /// </summary>
        /// <param name="index">The new position: 0 is the bottom, and the number of scene items of the scene minus one is the top.</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when <see cref="Index"/> is <paramref name="index"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative (thrown at once), or is not a position in the scene (the call ends with it, after the scene item is known to be there: a scene item that was removed is an <see cref="ObsRemovedException"/>, whatever the position).</exception>
        /// <exception cref="ObsRemovedException">The scene item, or its scene, was removed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the change, and the model does not show it: the events did not show it in time, and the model could not be read again in time. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        /// <remarks>
        /// The scene items between the old and the new position move by one place, and OBS Studio does not say so item by item: the model reads the scene items again when
        /// the events do not show the change in time (see <see cref="SetEnabledAsync"/>).
        /// </remarks>
        public Task SetIndexAsync(int index, CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            return this.Scene.Model.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = $"moving {this.Subject()} to position {index}",
                    Subject = this.Subject(),
                    SubjectGone = () => this.IsRemoved,
                    Check = () =>
                    {
                        this.CheckWritable();

                        // Whether it is a position in the scene is asked after the scene item is known to be there: what the model shows of a scene item that was removed (or of a scene
                        // that was destroyed) is a scene with fewer scene items, and that is no reason to blame the argument.
                        var count = this.Scene.Items.Count;
                        if (index >= Math.Max(count, 1))
                        {
                            throw new ArgumentOutOfRangeException(nameof(index), index, $"The scene '{this.Scene.Name}' has {count} scene items: a position is from 0 to {Math.Max(count, 1) - 1}.");
                        }
                    },
                    BuildRequests = () => [new WriteRequest(RequestType.SetSceneItemIndex, new { sceneUuid = this.Scene.Uuid, sceneItemId = this.Id, sceneItemIndex = index })],
                    IsConfirmed = _ => this.IsRemoved || this.Index == index,
                    Refresh = [new RefreshTarget(ReadKind.Items, this.Scene.Uuid)],
                },
                cancellationToken);
        }

        /// <summary>
        /// Removes the scene item from its scene, and returns when the model shows it.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when <see cref="IsRemoved"/> is true.</returns>
        /// <exception cref="ObsRemovedException">The scene item, or its scene, was removed already.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the change, and the model does not show it: the events did not show it in time, and the model could not be read again in time. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        /// <remarks>
        /// The source that the item shows is not removed, but OBS Studio keeps an input only for as long as something uses it: when this was the last scene item that showed the input,
        /// OBS Studio destroys the input with it (it is not in <see cref="ObsModel.Inputs"/> any more, and its <see cref="ObsSource.State"/> is <see cref="ObsObjectState.Gone"/>),
        /// whether the input was removed (<see cref="ObsObjectState.Detached"/>) or not. An input that something else holds, such as an audio device of the settings of OBS Studio, stays.
        /// </remarks>
        public Task RemoveAsync(CancellationToken cancellationToken = default)
        {
            return this.Scene.Model.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = $"removing {this.Subject()}",
                    Subject = this.Subject(),
                    SubjectGone = () => this.IsRemoved,
                    Check = this.CheckWritable,
                    BuildRequests = () => [new WriteRequest(RequestType.RemoveSceneItem, new { sceneUuid = this.Scene.Uuid, sceneItemId = this.Id })],
                    IsConfirmed = _ => this.IsRemoved,
                    Refresh = [new RefreshTarget(ReadKind.Items, this.Scene.Uuid)],
                },
                cancellationToken);
        }

        private string Subject()
        {
            return $"scene item {this.Id} of scene '{this.Scene.Name}'";
        }

        // A scene item can be changed while it is in its scene, and the scene is: a scene that OBS Studio removed and keeps alive does not take requests.
        private void CheckWritable()
        {
            if (this.IsRemoved)
            {
                throw new ObsRemovedException(this.Subject(), ObsObjectState.Gone);
            }

            if (this.Scene.State != ObsObjectState.Live)
            {
                throw new ObsRemovedException(this.Scene.Label, this.Scene.State);
            }
        }
    }
}
