namespace OBSStudioClient.Studio
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Studio.Engine;

    public sealed partial class ObsSceneItem
    {
        private SemaphoreSlim? _toggleGate;

        /// <summary>
        /// Shows or hides the scene item, and returns when the model shows it.
        /// </summary>
        /// <param name="value">True to show the item, false to hide it.</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when <see cref="IsEnabled"/> is <paramref name="value"/>, or when the model has been read again after the events did not show it.</returns>
        /// <exception cref="ObsRemovedException">The scene item, or its scene, was removed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        /// <remarks>
        /// <para>
        /// The changes that you ask for are made one at a time, in the order in which you asked for them. OBS Studio announces a change with an event, which can come a few
        /// milliseconds after its answer: the task completes when <see cref="IsEnabled"/> says what you asked for, so that what you read next is what you changed. When the
        /// event does not come within <see cref="ObsStudioOptions.ConfirmationTimeout"/>, the model reads the scene items of the scene, and the task completes with what
        /// OBS Studio holds then (somebody else may have changed it again). When OBS Studio changes its scene collection before the model shows the change, the task completes
        /// too: the change was made in the collection that was left, and this object is <see cref="ObsObjectState.Gone"/> by then.
        /// </para>
        /// <para>
        /// A token that ends before the request is sent keeps it from being sent. After that the change is not undone: OBS Studio may still make it, and the model follows it
        /// through the events.
        /// </para>
        /// </remarks>
        public Task SetEnabledAsync(bool value, CancellationToken cancellationToken = default)
        {
            return this.Scene.Studio.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = $"{(value ? "showing" : "hiding")} {this.Subject()}",
                    Subject = this.Subject(),
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
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        /// <remarks>
        /// OBS Studio has no request that toggles a scene item, so this reads <see cref="IsEnabled"/> and sets the opposite. Two calls on the same item do not run
        /// together: the second one waits until the model shows the result of the first, so that two calls always make two toggles.
        /// </remarks>
        public async Task<bool> ToggleEnabledAsync(CancellationToken cancellationToken = default)
        {
            var gate = LazyInitializer.EnsureInitialized(ref this._toggleGate, static () => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var value = !this.IsEnabled;
                await this.SetEnabledAsync(value, cancellationToken).ConfigureAwait(false);
                return value;
            }
            finally
            {
                gate.Release();
            }
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
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        /// <remarks>See <see cref="SetEnabledAsync"/> for the order of changes and the wait.</remarks>
        public Task SetLockedAsync(bool value, CancellationToken cancellationToken = default)
        {
            return this.Scene.Studio.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = $"{(value ? "locking" : "unlocking")} {this.Subject()}",
                    Subject = this.Subject(),
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
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not a position in the scene.</exception>
        /// <exception cref="ObsRemovedException">The scene item, or its scene, was removed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        /// <remarks>
        /// The scene items between the old and the new position move by one place, and OBS Studio does not say so item by item: the model reads the scene items again when
        /// the events do not show the change in time (see <see cref="SetEnabledAsync"/>).
        /// </remarks>
        public Task SetIndexAsync(int index, CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Math.Max(this.Scene.Items.Count, 1));
            return this.Scene.Studio.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = $"moving {this.Subject()} to position {index}",
                    Subject = this.Subject(),
                    Check = this.CheckWritable,
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
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        /// <remarks>
        /// The source that the item shows is not removed. When it was the last scene item that showed an input that OBS Studio keeps alive (<see cref="ObsObjectState.Detached"/>),
        /// OBS Studio destroys the input with it.
        /// </remarks>
        public Task RemoveAsync(CancellationToken cancellationToken = default)
        {
            return this.Scene.Studio.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = $"removing {this.Subject()}",
                    Subject = this.Subject(),
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
                throw new ObsRemovedException($"scene '{this.Scene.Name}'", this.Scene.State);
            }
        }
    }
}
