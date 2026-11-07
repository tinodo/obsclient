namespace OBSStudioClient.Studio
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Responses;
    using OBSStudioClient.Studio.Engine;

    public sealed partial class ObsInput
    {
        // The volume that OBS Studio keeps is a multiplier, and the decibels that it reports are made from it: a volume that was set in decibels comes back a hair
        // different, and the confirmation compares within this.
        private const float VolumeDbTolerance = 0.01f;
        private const double VolumeMulTolerance = 0.0001;

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
                    IsConfirmed = _ => this.IsRemoved || Math.Abs(this.VolumeMul - multiplier) <= VolumeMulTolerance * Math.Max(1d, multiplier),
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
    }
}
