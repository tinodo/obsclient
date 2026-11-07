namespace OBSStudioClient.Studio
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Responses;
    using OBSStudioClient.Studio.Engine;

    public sealed partial class ObsInput
    {
        // The volume that OBS Studio keeps is a multiplier, and the decibels that it reports are made from it: a volume that was set in decibels comes back a hair
        // different, and the confirmation compares within this. For a multiplier the tolerance is relative (a volume near silence must not be taken for another one that is
        // a few decibels away), and for zero (silence, which OBS Studio reports as exactly 0) it is the floor.
        private const float VolumeDbTolerance = 0.01f;
        private const double VolumeMulRelativeTolerance = 0.0001;
        private const double VolumeMulFloor = 1e-9;

        /// <summary>
        /// Mutes or unmutes the input, and returns when the model shows it.
        /// </summary>
        /// <param name="muted">True to mute the input, false to unmute it.</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when <see cref="IsMuted"/> is <paramref name="muted"/>, or when the model has been read again after the events did not show it.</returns>
        /// <exception cref="InvalidOperationException">The input has no audio (<see cref="HasAudio"/>).</exception>
        /// <exception cref="ObsRemovedException">The input was removed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the change, and the model does not show it: the events did not show it in time, and the model could not be read again in time. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        /// <remarks>See <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes and the wait.</remarks>
        public Task SetMutedAsync(bool muted, CancellationToken cancellationToken = default)
        {
            return this.Studio.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = $"{(muted ? "muting" : "unmuting")} input '{this.Name}'",
                    Subject = $"input '{this.Name}'",
                    Check = this.CheckWritableAudio,
                    BuildRequests = () => [new WriteRequest(RequestType.SetInputMute, new { inputUuid = this.Uuid, inputMuted = muted })],
                    IsConfirmed = _ => this.IsRemoved || this.IsMuted == muted,
                    Refresh = [new RefreshTarget(ReadKind.AudioMute, this.Uuid)],
                },
                cancellationToken);
        }

        /// <summary>
        /// Mutes the input when it is not muted, and unmutes it when it is, and returns when the model shows it.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes with the value that <see cref="IsMuted"/> was set to.</returns>
        /// <exception cref="InvalidOperationException">The input has no audio (<see cref="HasAudio"/>).</exception>
        /// <exception cref="ObsRemovedException">The input was removed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the change, and the model does not show it: the events did not show it in time, and the model could not be read again in time. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        /// <remarks>
        /// OBS Studio toggles the state by itself, so two toggles that are asked for one after the other make two toggles, whatever the model shows meanwhile.
        /// See <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes and the wait.
        /// </remarks>
        public Task<bool> ToggleMuteAsync(CancellationToken cancellationToken = default)
        {
            return this.Studio.Writer.ExecuteAsync<bool>(
                new WriteJob
                {
                    Description = $"toggling the mute of input '{this.Name}'",
                    Subject = $"input '{this.Name}'",
                    Check = this.CheckWritableAudio,
                    BuildRequests = () => [new WriteRequest(RequestType.ToggleInputMute, new { inputUuid = this.Uuid })],
                    ReadResponses = static responses => ((InputMutedResponse)responses[0].ResponseData!).InputMuted,
                    IsConfirmed = result => this.IsRemoved || this.IsMuted == (bool)result!,
                    Refresh = [new RefreshTarget(ReadKind.AudioMute, this.Uuid)],
                },
                cancellationToken);
        }

        /// <summary>
        /// Sets the volume of the input in decibels, and returns when the model shows it.
        /// </summary>
        /// <param name="decibels">The volume: from -100 dB (silent) to 26 dB. 0 dB is the volume of the source.</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when <see cref="VolumeDb"/> is <paramref name="decibels"/> (within the precision of OBS Studio).</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="decibels"/> is not from -100 to 26.</exception>
        /// <exception cref="InvalidOperationException">The input has no audio (<see cref="HasAudio"/>).</exception>
        /// <exception cref="ObsRemovedException">The input was removed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the change, and the model does not show it: the events did not show it in time, and the model could not be read again in time. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        /// <remarks>See <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes and the wait.</remarks>
        public Task SetVolumeDbAsync(double decibels, CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(decibels, -100d);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(decibels, 26d);
            return this.Studio.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = $"setting the volume of input '{this.Name}' to {decibels} dB",
                    Subject = $"input '{this.Name}'",
                    Check = this.CheckWritableAudio,
                    BuildRequests = () => [new WriteRequest(RequestType.SetInputVolume, new { inputUuid = this.Uuid, inputVolumeDb = decibels })],
                    IsConfirmed = _ => this.IsRemoved || Math.Abs(this.VolumeDb - decibels) <= VolumeDbTolerance,
                    Refresh = [new RefreshTarget(ReadKind.AudioMute, this.Uuid)],
                },
                cancellationToken);
        }

        /// <summary>
        /// Sets the volume of the input as a multiplier, and returns when the model shows it.
        /// </summary>
        /// <param name="multiplier">The volume: from 0 (silent) to 20. 1 is the volume of the source (0 dB).</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when <see cref="VolumeMul"/> is <paramref name="multiplier"/> (within the precision of OBS Studio).</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="multiplier"/> is not from 0 to 20.</exception>
        /// <exception cref="InvalidOperationException">The input has no audio (<see cref="HasAudio"/>).</exception>
        /// <exception cref="ObsRemovedException">The input was removed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the change, and the model does not show it: the events did not show it in time, and the model could not be read again in time. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        /// <remarks>See <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes and the wait.</remarks>
        public Task SetVolumeMulAsync(double multiplier, CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(multiplier, 0d);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(multiplier, 20d);
            return this.Studio.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = $"setting the volume of input '{this.Name}' to {multiplier}",
                    Subject = $"input '{this.Name}'",
                    Check = this.CheckWritableAudio,
                    BuildRequests = () => [new WriteRequest(RequestType.SetInputVolume, new { inputUuid = this.Uuid, inputVolumeMul = multiplier })],
                    IsConfirmed = _ => this.IsRemoved || Math.Abs(this.VolumeMul - multiplier) <= Math.Max(VolumeMulRelativeTolerance * multiplier, VolumeMulFloor),
                    Refresh = [new RefreshTarget(ReadKind.AudioMute, this.Uuid)],
                },
                cancellationToken);
        }

        // An input that was removed does not take changes (a scene or an input that OBS Studio keeps alive can only be removed for good), and the audio of an input
        // that has none can not be changed.
        private void CheckWritableAudio()
        {
            if (this.State != ObsObjectState.Live)
            {
                throw new ObsRemovedException($"input '{this.Name}'", this.State);
            }

            if (!this.HasAudio)
            {
                throw new InvalidOperationException($"The input '{this.Name}' has no audio (its kind is {this.Kind}).");
            }
        }

        /// <summary>
        /// Gives the input another name, and returns when the model shows it.
        /// </summary>
        /// <param name="name">The new name: it can not be the name of another input, or of a scene, or of an input that was removed and that OBS Studio keeps alive (the name of any other source). The names are case sensitive.</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when <see cref="ObsSource.Name"/> is <paramref name="name"/>.</returns>
        /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
        /// <exception cref="ObsNameInUseException">The name is taken, also when it is the name that the input has already. Nothing was changed.</exception>
        /// <exception cref="ObsRemovedException">The input was removed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the change, and the model does not show it. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        /// <remarks>See <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes and the wait.</remarks>
        public Task RenameAsync(string name, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            var description = $"renaming input '{this.Name}' to '{name}'";
            var studio = this.Studio;
            return studio.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = description,
                    Subject = $"input '{this.Name}'",
                    Check = this.CheckLive,
                    BuildRequests = () => [new WriteRequest(RequestType.SetInputName, new { inputUuid = this.Uuid, newInputName = name })],
                    IsConfirmed = _ => this.IsRemoved || string.Equals(this.Name, name, StringComparison.Ordinal),
                    Refresh = [new RefreshTarget(ReadKind.Inputs)],
                    Map = (_, response) => response.RequestStatus.Code == RequestStatusCode.ResourceAlreadyExists
                        ? StudioNames.InUse(studio, description, name, forScene: false, requester: this)
                        : null,
                },
                cancellationToken);
        }

        /// <summary>
        /// Removes the input, and the scene items that show it, and returns when the model shows it.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when the input is removed (<see cref="ObsSource.State"/> is not <see cref="ObsObjectState.Live"/>) and the scene items that were showing it are gone.</returns>
        /// <exception cref="InvalidOperationException">A scene item in a group shows the input: obs-websocket removes the scene items of scenes only, not those of a group, so the input can not be removed for good from here (take it out of the group in OBS Studio first). Nothing was changed.</exception>
        /// <exception cref="ObsRemovedException">The input does not exist any more.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the change, and the model does not show it. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        /// <remarks>
        /// <para>
        /// OBS Studio keeps an input alive after it is removed for as long as scene items show it: the input is not in <see cref="ObsStudio.Inputs"/> then (it is
        /// <see cref="ObsObjectState.Detached"/>), its scene items stay in their scenes and keep working, and its name stays taken. This call removes the input and then the scene items
        /// that show it, one request after the other, so that OBS Studio destroys it (<see cref="ObsObjectState.Gone"/>) and the name is free. Calling it on an input that is Detached
        /// removes the scene items that are left. When OBS Studio refuses one of the scene items (it was removed meanwhile: by the user, or by OBS Studio itself, which takes the scene items
        /// of a removed input out of the scenes that it is rendering on the next frame, so a scene item can be gone between two requests of this call), the call ends with the exception once
        /// the model shows what the requests before it made (the input is Detached, and the scene items that were removed are gone), so that calling <see cref="RemoveAsync"/> again removes what is left. An input that a scene item shows that the model did not know yet (OBS Studio announces
        /// it a few milliseconds late) stays Detached in the same way.
        /// </para>
        /// <para>
        /// The scene items are the ones that the model shows when the change has its turn (see <see cref="ObsSource.GetSceneItems"/>). See <see cref="ObsSceneItem.SetEnabledAsync"/>
        /// for the order of changes and the wait.
        /// </para>
        /// </remarks>
        public Task RemoveAsync(CancellationToken cancellationToken = default)
        {
            var description = $"removing input '{this.Name}'";
            ObsSceneItem[] showing = [];
            var includesRemoval = false;
            return this.Studio.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = description,
                    Subject = $"input '{this.Name}'",
                    Check = this.CheckCanBeRemoved,
                    BuildRequests = () =>
                    {
                        showing = this.GetSceneItems().Where(item => !item.IsRemoved).ToArray();
                        includesRemoval = this.State == ObsObjectState.Live;
                        return StudioRemovals.Requests(includesRemoval ? new WriteRequest(RequestType.RemoveInput, new { inputUuid = this.Uuid }) : null, showing);
                    },
                    ReadResponses = _ => showing,
                    IsConfirmed = result => this.IsRemoved && ((ObsSceneItem[])result!).All(item => item.IsRemoved),
                    Finish = static _ => null,
                    Refresh = [new RefreshTarget(ReadKind.Inputs)],
                    RefreshAtTurn = () => [new RefreshTarget(ReadKind.Inputs), .. this.GetSceneItems().Select(item => new RefreshTarget(ReadKind.Items, item.Scene.Uuid)).Distinct()],
                    Map = (index, response) => StudioRemovals.Refusal(description, includesRemoval, showing, index, response),
                    Reconcile = index => StudioRemovals.Reconciled(this, includesRemoval, showing, index),
                },
                cancellationToken);
        }

        /// <summary>
        /// Shows or hides the input in every scene that shows it, and returns when the model shows it.
        /// </summary>
        /// <param name="value">True to show the scene items of the input, false to hide them.</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when <see cref="ObsSceneItem.IsEnabled"/> is <paramref name="value"/> for each scene item that was showing the input.</returns>
        /// <exception cref="ObsRemovedException">The input was removed, or OBS Studio refused one of its scene items because it was removed meanwhile.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the change, and the model does not show it. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        /// <remarks>
        /// <para>
        /// OBS Studio has no request for this: the call changes each scene item that shows the input, in one batch that stops at its first refusal (the scene items before it were
        /// changed). The scene items are the ones that the model shows when the change has its turn, in scenes that are not removed (see <see cref="ObsSource.GetSceneItems"/>); an input that
        /// no scene item shows has nothing to change, and the call returns when it has its turn. See <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes and the wait.
        /// </para>
        /// </remarks>
        public Task SetEnabledInAllScenesAsync(bool value, CancellationToken cancellationToken = default)
        {
            var description = $"{(value ? "showing" : "hiding")} input '{this.Name}' in every scene";
            ObsSceneItem[] targets = [];
            return this.Studio.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = description,
                    Subject = $"input '{this.Name}'",
                    Check = this.CheckLive,
                    BuildRequests = () =>
                    {
                        targets = this.GetSceneItems().Where(item => !item.IsRemoved && item.Scene.State == ObsObjectState.Live).ToArray();
                        return targets.Select(item => new WriteRequest(RequestType.SetSceneItemEnabled, new { sceneUuid = item.Scene.Uuid, sceneItemId = item.Id, sceneItemEnabled = value })).ToArray();
                    },
                    ReadResponses = _ => targets,
                    IsConfirmed = result => this.IsRemoved || ((ObsSceneItem[])result!).All(item => item.IsRemoved || item.IsEnabled == value),
                    Finish = static _ => null,
                    RefreshAtTurn = () => [.. this.GetSceneItems().Select(item => new RefreshTarget(ReadKind.Items, item.Scene.Uuid)).Distinct()],
                    Map = (index, response) => response.RequestStatus.Code == RequestStatusCode.ResourceNotFound && index < targets.Length
                        ? new ObsRemovedException($"scene item {targets[index].Id} of scene '{targets[index].Scene.Name}'", ObsObjectState.Gone)
                        : null,
                },
                cancellationToken);
        }

        private void CheckLive()
        {
            if (this.State != ObsObjectState.Live)
            {
                throw new ObsRemovedException($"input '{this.Name}'", this.State);
            }
        }

        // An input that was removed and that OBS Studio keeps alive can be removed for good (its scene items), and one that is gone can not be removed. Nor can one that a group shows.
        private void CheckCanBeRemoved()
        {
            if (this.State == ObsObjectState.Gone)
            {
                throw new ObsRemovedException($"input '{this.Name}'", this.State);
            }

            StudioRemovals.ThrowIfAGroupShows(this);
        }
    }
}
