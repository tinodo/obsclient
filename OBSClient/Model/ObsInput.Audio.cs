namespace OBSStudioClient.Model
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Model.Engine;
    using OBSStudioClient.Responses;

    // What can be changed about the audio of an input beyond its mute and its volume (ObsInput.Writes.cs), and the deinterlacing of its video.
    public sealed partial class ObsInput
    {
        private const float BalanceTolerance = 0.000001f;

        /// <summary>
        /// Sets the balance of the audio of the input between the left and the right, and returns when the model shows it.
        /// </summary>
        /// <param name="balance">The balance: from 0 (all on the left) to 1 (all on the right). 0.5 is the middle.</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when <see cref="AudioBalance"/> is <paramref name="balance"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="balance"/> is not from 0 to 1.</exception>
        /// <exception cref="InvalidOperationException">The input has no audio (<see cref="HasAudio"/>).</exception>
        /// <exception cref="ObsRemovedException">The input was removed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the change, and the model does not show it: the events did not show it in time, and the model could not be read again in time. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        /// <remarks>See <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes and the wait.</remarks>
        public Task SetAudioBalanceAsync(float balance, CancellationToken cancellationToken = default)
        {
            if (float.IsNaN(balance))
            {
                throw new ArgumentOutOfRangeException(nameof(balance), balance, "The balance is a number from 0 to 1.");
            }

            ArgumentOutOfRangeException.ThrowIfLessThan(balance, 0f);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(balance, 1f);
            return this.Model.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = $"setting the audio balance of input '{this.Name}' to {balance}",
                    Subject = $"input '{this.Name}'",
                    SubjectGone = () => this.IsRemoved,
                    Check = this.CheckWritableAudio,
                    BuildRequests = () => [new WriteRequest(RequestType.SetInputAudioBalance, new { inputUuid = this.Uuid, inputAudioBalance = balance })],
                    IsConfirmed = _ => this.IsRemoved || Math.Abs(this.AudioBalance - balance) <= BalanceTolerance,
                    Refresh = [new RefreshTarget(ReadKind.AudioMute, this.Uuid)],
                },
                cancellationToken);
        }

        /// <summary>
        /// Sets how much the audio of the input is delayed or advanced, and returns when the model shows it.
        /// </summary>
        /// <param name="offset">The offset: from -950 ms to 20 s, in whole milliseconds (a part of a millisecond is rounded). A positive offset delays the audio, a negative one advances it.</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when <see cref="SyncOffset"/> is <paramref name="offset"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="offset"/> is not from -950 ms to 20 s.</exception>
        /// <exception cref="InvalidOperationException">The input has no audio (<see cref="HasAudio"/>).</exception>
        /// <exception cref="ObsRemovedException">The input was removed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the change, and the model does not show it: the events did not show it in time, and the model could not be read again in time. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        /// <remarks>See <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes and the wait.</remarks>
        public Task SetSyncOffsetAsync(TimeSpan offset, CancellationToken cancellationToken = default)
        {
            var milliseconds = (int)Math.Round(offset.TotalMilliseconds);
            if (milliseconds < -950 || milliseconds > 20000)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), offset, "The sync offset is from -950 milliseconds to 20 seconds.");
            }

            return this.Model.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = $"setting the sync offset of input '{this.Name}' to {milliseconds} ms",
                    Subject = $"input '{this.Name}'",
                    SubjectGone = () => this.IsRemoved,
                    Check = this.CheckWritableAudio,
                    BuildRequests = () => [new WriteRequest(RequestType.SetInputAudioSyncOffset, new { inputUuid = this.Uuid, inputAudioSyncOffset = milliseconds })],
                    IsConfirmed = _ => this.IsRemoved || (int)this.SyncOffset.TotalMilliseconds == milliseconds,
                    Refresh = [new RefreshTarget(ReadKind.AudioMute, this.Uuid)],
                },
                cancellationToken);
        }

        /// <summary>
        /// Sets whether the audio of the input is also played on the monitoring device, and returns when the model shows it.
        /// </summary>
        /// <param name="type">What to do with the audio.</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when <see cref="MonitorType"/> is <paramref name="type"/>, or when the model has been read again after OBS Studio held another type (see the remarks).</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> is not a value of <see cref="ObsMonitorType"/>.</exception>
        /// <exception cref="InvalidOperationException">The input has no audio (<see cref="HasAudio"/>).</exception>
        /// <exception cref="ObsRemovedException">The input was removed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the change, and the model does not show it: the events did not show it in time, and the model could not be read again in time. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change: the computer of OBS Studio has no audio monitoring (<see cref="ObsResponseException.ErrorCode"/> is <see cref="RequestStatusCode.InvalidResourceState"/>).</exception>
        /// <remarks>
        /// OBS Studio 33 has no <see cref="ObsMonitorType.MonitorOnly"/> (see <see cref="ObsMonitorType"/>): it holds <see cref="ObsMonitorType.MonitorAndOutput"/> after it was asked for it, so the call
        /// returns after the model has been read again (about <see cref="ObsModelOptions.ConfirmationTimeout"/>), with <see cref="MonitorType"/> showing what OBS Studio holds, and no exception. Compare <see cref="MonitorType"/>
        /// with what you asked for if that matters. See <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes and the wait.
        /// </remarks>
        public Task SetMonitorTypeAsync(ObsMonitorType type, CancellationToken cancellationToken = default)
        {
            if (!Enum.IsDefined(type))
            {
                throw new ArgumentOutOfRangeException(nameof(type), type, "The monitor type is one of the values of ObsMonitorType.");
            }

            return this.Model.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = $"setting the monitor type of input '{this.Name}' to {type}",
                    Subject = $"input '{this.Name}'",
                    SubjectGone = () => this.IsRemoved,
                    Check = this.CheckWritableAudio,
                    BuildRequests = () => [new WriteRequest(RequestType.SetInputAudioMonitorType, new { inputUuid = this.Uuid, monitorType = ModelEnums.ToWire(type).ToString() })],
                    IsConfirmed = _ => this.IsRemoved || this.MonitorType == type,
                    Refresh = [new RefreshTarget(ReadKind.AudioMute, this.Uuid)],
                },
                cancellationToken);
        }

        /// <summary>
        /// Sets the audio tracks that the input is mixed into, and returns when the model shows it.
        /// </summary>
        /// <param name="tracks">The tracks: exactly these, and no others. To change one track and keep the others, start from <see cref="AudioTracks"/>: <c>input.SetAudioTracksAsync(input.AudioTracks | ObsAudioTracks.Track2)</c>.</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when <see cref="AudioTracks"/> is <paramref name="tracks"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="tracks"/> has a bit that is none of the six tracks.</exception>
        /// <exception cref="InvalidOperationException">The input has no audio (<see cref="HasAudio"/>).</exception>
        /// <exception cref="ObsRemovedException">The input was removed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the change, and the model does not show it: the events did not show it in time, and the model could not be read again in time. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        /// <remarks>
        /// All six tracks are sent: the call is about the state that you give, and does not depend on what another change that is not shown yet will have made. See <see cref="ObsSceneItem.SetEnabledAsync"/>
        /// for the order of changes and the wait.
        /// </remarks>
        public Task SetAudioTracksAsync(ObsAudioTracks tracks, CancellationToken cancellationToken = default)
        {
            if ((tracks & ~ObsAudioTracks.All) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tracks), tracks, "The tracks are the six tracks of ObsAudioTracks.");
            }

            return this.Model.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = $"setting the audio tracks of input '{this.Name}' to {tracks}",
                    Subject = $"input '{this.Name}'",
                    SubjectGone = () => this.IsRemoved,
                    Check = this.CheckWritableAudio,
                    BuildRequests = () => [new WriteRequest(RequestType.SetInputAudioTracks, new { inputUuid = this.Uuid, inputAudioTracks = ModelEnums.ToWire(tracks) })],
                    IsConfirmed = _ => this.IsRemoved || this.AudioTracks == tracks,
                    Refresh = [new RefreshTarget(ReadKind.AudioMute, this.Uuid)],
                },
                cancellationToken);
        }

        /// <summary>
        /// Asks OBS Studio how the video of the input is deinterlaced.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A request that was not sent yet is not sent.</param>
        /// <returns>The mode.</returns>
        /// <exception cref="ObsRemovedException">The input was destroyed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call.</exception>
        /// <exception cref="TimeoutException">OBS Studio did not answer in time (<see cref="ObsModelOptions.WriteTimeout"/>).</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the request: the input is not an async input (<see cref="ObsResponseException.ErrorCode"/> is <see cref="RequestStatusCode.InvalidResourceState"/>), or its obs-websocket is older than 5.6.0.</exception>
        /// <remarks>
        /// OBS Studio does not announce a change of the deinterlacing, so the model does not keep it: ask when you need it. Only inputs that deliver their frames from outside (capture devices,
        /// media) can be deinterlaced. The request is made in its turn, after the changes that were asked for before it.
        /// </remarks>
        public Task<ObsDeinterlaceMode> GetDeinterlaceModeAsync(CancellationToken cancellationToken = default)
        {
            return this.Model.Writer.ExecuteAsync<ObsDeinterlaceMode>(
                new WriteJob
                {
                    Description = $"asking for the deinterlace mode of {this.Label}",
                    Subject = this.Label,
                    ReadOnly = true,
                    Check = this.CheckNotDestroyed,
                    BuildRequests = () => [new WriteRequest(RequestType.GetInputDeinterlaceMode, new { inputUuid = this.Uuid })],
                    ReadResponses = static responses => ModelEnums.ToModel(((InputDeinterlaceModeResponse)responses[0].ResponseData!).InputDeinterlaceMode),
                },
                cancellationToken);
        }

        /// <summary>
        /// Sets how the video of the input is deinterlaced, and returns when OBS Studio has answered.
        /// </summary>
        /// <param name="mode">The mode.</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when OBS Studio has answered.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is not a value of <see cref="ObsDeinterlaceMode"/>.</exception>
        /// <exception cref="ObsRemovedException">The input was destroyed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change: the input is not an async input (<see cref="ObsResponseException.ErrorCode"/> is <see cref="RequestStatusCode.InvalidResourceState"/>), or its obs-websocket is older than 5.6.0.</exception>
        /// <remarks>See <see cref="GetDeinterlaceModeAsync"/> for what to know about the deinterlacing, and <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes.</remarks>
        public Task SetDeinterlaceModeAsync(ObsDeinterlaceMode mode, CancellationToken cancellationToken = default)
        {
            if (!Enum.IsDefined(mode))
            {
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "The deinterlace mode is one of the values of ObsDeinterlaceMode.");
            }

            return this.Model.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = $"setting the deinterlace mode of {this.Label} to {mode}",
                    Subject = this.Label,
                    Check = this.CheckNotDestroyed,
                    BuildRequests = () => [new WriteRequest(RequestType.SetInputDeinterlaceMode, new { inputUuid = this.Uuid, inputDeinterlaceMode = ModelEnums.ToWire(mode).ToString() })],
                },
                cancellationToken);
        }

        /// <summary>
        /// Asks OBS Studio which field of an interlaced frame of the input comes first.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A request that was not sent yet is not sent.</param>
        /// <returns>The field order.</returns>
        /// <exception cref="ObsRemovedException">The input was destroyed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call.</exception>
        /// <exception cref="TimeoutException">OBS Studio did not answer in time (<see cref="ObsModelOptions.WriteTimeout"/>).</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the request: the input is not an async input (<see cref="ObsResponseException.ErrorCode"/> is <see cref="RequestStatusCode.InvalidResourceState"/>), or its obs-websocket is older than 5.6.0.</exception>
        /// <remarks>See <see cref="GetDeinterlaceModeAsync"/> for what to know about the deinterlacing.</remarks>
        public Task<ObsDeinterlaceFieldOrder> GetDeinterlaceFieldOrderAsync(CancellationToken cancellationToken = default)
        {
            return this.Model.Writer.ExecuteAsync<ObsDeinterlaceFieldOrder>(
                new WriteJob
                {
                    Description = $"asking for the deinterlace field order of {this.Label}",
                    Subject = this.Label,
                    ReadOnly = true,
                    Check = this.CheckNotDestroyed,
                    BuildRequests = () => [new WriteRequest(RequestType.GetInputDeinterlaceFieldOrder, new { inputUuid = this.Uuid })],
                    ReadResponses = static responses => ModelEnums.ToModelFieldOrder(((InputDeinterlaceFieldOrderResponse)responses[0].ResponseData!).InputDeinterlaceFieldOrder),
                },
                cancellationToken);
        }

        /// <summary>
        /// Sets which field of an interlaced frame of the input comes first, and returns when OBS Studio has answered.
        /// </summary>
        /// <param name="order">The field order.</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when OBS Studio has answered.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="order"/> is not a value of <see cref="ObsDeinterlaceFieldOrder"/>.</exception>
        /// <exception cref="ObsRemovedException">The input was destroyed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change: the input is not an async input (<see cref="ObsResponseException.ErrorCode"/> is <see cref="RequestStatusCode.InvalidResourceState"/>), or its obs-websocket is older than 5.6.0.</exception>
        /// <remarks>See <see cref="GetDeinterlaceModeAsync"/> for what to know about the deinterlacing, and <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes.</remarks>
        public Task SetDeinterlaceFieldOrderAsync(ObsDeinterlaceFieldOrder order, CancellationToken cancellationToken = default)
        {
            if (!Enum.IsDefined(order))
            {
                throw new ArgumentOutOfRangeException(nameof(order), order, "The field order is one of the values of ObsDeinterlaceFieldOrder.");
            }

            return this.Model.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = $"setting the deinterlace field order of {this.Label} to {order}",
                    Subject = this.Label,
                    Check = this.CheckNotDestroyed,
                    BuildRequests = () => [new WriteRequest(RequestType.SetInputDeinterlaceFieldOrder, new { inputUuid = this.Uuid, inputDeinterlaceFieldOrder = ModelEnums.ToWire(order) })],
                },
                cancellationToken);
        }
    }
}
