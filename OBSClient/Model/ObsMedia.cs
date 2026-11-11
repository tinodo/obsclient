namespace OBSStudioClient.Model
{
    using System.ComponentModel;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Model.Engine;
    using OBSStudioClient.Responses;

    /// <summary>
    /// The playback of a media input of OBS Studio (a media source or a VLC video source): what it is doing, and the calls that play, pause, stop, restart and seek it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A media input has one: <see cref="ObsInput.Media"/> is null for an input that is no media input. The object stays the same for as long as the input exists.
    /// </para>
    /// <para>
    /// <see cref="State"/> is kept current by the events that OBS Studio sends about playback, and the calls that change it return when the model shows the change (see
    /// <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes and the wait). The position and the length of the media are not in the model: they move all the time, and OBS Studio
    /// sends no event for them, so ask <see cref="GetStatusAsync"/> when you need them.
    /// </para>
    /// <para>
    /// What OBS Studio does (measured with OBS Studio 33.0.0): a media source that restarts when it is shown (the default) is not opened while its scene is not on the program output: its state is
    /// <see cref="ObsMediaState.None"/> until it is told to play or pause, or until the scene is shown. Showing the scene plays it from the start, and when it is not shown any more it ends (unless the setting of
    /// the source says otherwise). A source that does not restart when it is shown is opened, and plays, as soon as it is made. The model follows all of it like any other change of the state.
    /// </para>
    /// <para>
    /// The position changes only for a media that plays and is shown, see <see cref="SeekAsync"/>.
    /// </para>
    /// </remarks>
    public sealed class ObsMedia : INotifyPropertyChanged
    {
        private int _state;

        internal ObsMedia(ObsInput input, ObsMediaState state)
        {
            this.Input = input;
            this._state = (int)state;
        }

        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Gets the input that plays the media.
        /// </summary>
        public ObsInput Input { get; }

        /// <summary>
        /// Gets what the media is doing. The value is current: OBS Studio announces what matters (it plays, it is paused or stopped, it ended). It does not show <see cref="ObsMediaState.Opening"/>,
        /// <see cref="ObsMediaState.Buffering"/> and <see cref="ObsMediaState.Error"/> after the model has read it, because OBS Studio does not announce them: see <see cref="GetStatusAsync"/>.
        /// </summary>
        public ObsMediaState State => (ObsMediaState)Volatile.Read(ref this._state);

        /// <summary>
        /// Plays the media, and returns when the model shows that it plays.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when <see cref="State"/> is <see cref="ObsMediaState.Playing"/>.</returns>
        /// <exception cref="ObsRemovedException">The input was removed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the change, and the model does not show it: the events did not show it in time, and the model could not be read again in time. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        /// <remarks>
        /// A paused media goes on, and a media that was stopped or ended starts from the beginning. OBS Studio answers when it was told: a file that it can not open is not known yet then, and
        /// <see cref="State"/> says <see cref="ObsMediaState.Playing"/> all the same (ask <see cref="GetStatusAsync"/> to know what OBS Studio holds).
        /// </remarks>
        public Task PlayAsync(CancellationToken cancellationToken = default)
        {
            return this.ActAsync(ObsMediaInputAction.OBS_WEBSOCKET_MEDIA_INPUT_ACTION_PLAY, ObsMediaState.Playing, "playing", cancellationToken);
        }

        /// <summary>
        /// Pauses the media, and returns when the model shows that it is paused.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when <see cref="State"/> is <see cref="ObsMediaState.Paused"/>.</returns>
        /// <exception cref="ObsRemovedException">The input was removed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the change, and the model does not show it: the events did not show it in time, and the model could not be read again in time. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        /// <remarks>OBS Studio pauses a media that was stopped as well: it opens it and holds it at the beginning.</remarks>
        public Task PauseAsync(CancellationToken cancellationToken = default)
        {
            return this.ActAsync(ObsMediaInputAction.OBS_WEBSOCKET_MEDIA_INPUT_ACTION_PAUSE, ObsMediaState.Paused, "pausing", cancellationToken);
        }

        /// <summary>
        /// Stops the media, and returns when the model shows that it is stopped.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when <see cref="State"/> is <see cref="ObsMediaState.Stopped"/>.</returns>
        /// <exception cref="ObsRemovedException">The input was removed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the change, and the model does not show it: the events did not show it in time, and the model could not be read again in time. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            return this.ActAsync(ObsMediaInputAction.OBS_WEBSOCKET_MEDIA_INPUT_ACTION_STOP, ObsMediaState.Stopped, "stopping", cancellationToken);
        }

        /// <summary>
        /// Plays the media from the beginning, and returns when the model shows that it plays.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when <see cref="State"/> is <see cref="ObsMediaState.Playing"/>.</returns>
        /// <exception cref="ObsRemovedException">The input was removed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the change, and the model does not show it: the events did not show it in time, and the model could not be read again in time. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        public Task RestartAsync(CancellationToken cancellationToken = default)
        {
            return this.ActAsync(ObsMediaInputAction.OBS_WEBSOCKET_MEDIA_INPUT_ACTION_RESTART, ObsMediaState.Playing, "restarting", cancellationToken);
        }

        /// <summary>
        /// Pauses the media when it plays, and plays it when it does not (it is paused, stopped, ended or not opened), and returns when the model shows it.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes with the state that was asked for: <see cref="ObsMediaState.Playing"/> or <see cref="ObsMediaState.Paused"/>.</returns>
        /// <exception cref="ObsRemovedException">The input was removed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the change, and the model does not show it: the events did not show it in time, and the model could not be read again in time. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        /// <remarks>
        /// OBS Studio has no request that toggles, so what the media does is asked in the turn of the change, when every change that was asked for before it has been answered: two toggles that are
        /// asked for one after the other make two toggles, whatever <see cref="State"/> shows meanwhile. A media that is opening or buffering is taken for playing.
        /// </remarks>
        public Task<ObsMediaState> TogglePlayPauseAsync(CancellationToken cancellationToken = default)
        {
            var input = this.Input;

            // What OBS Studio holds when the turn of this change has come.
            var play = true;
            return input.Model.Writer.ExecuteAsync<ObsMediaState>(
                new WriteJob
                {
                    Description = $"toggling the playback of the media of {input.Label}",
                    Subject = input.Label,
                    SubjectGone = () => input.IsRemoved,
                    Check = this.CheckWritable,
                    Prepare = async ask =>
                    {
                        var answer = await ask(new WriteRequest(RequestType.GetMediaInputStatus, new { inputUuid = input.Uuid })).ConfigureAwait(false);
                        var state = ((MediaInputStatusResponse)answer.ResponseData!).MediaState;
                        play = state is not (MediaState.OBS_MEDIA_STATE_PLAYING or MediaState.OBS_MEDIA_STATE_OPENING or MediaState.OBS_MEDIA_STATE_BUFFERING);
                    },
                    BuildRequests = () => [new WriteRequest(RequestType.TriggerMediaInputAction, new { inputUuid = input.Uuid, mediaAction = (play ? ObsMediaInputAction.OBS_WEBSOCKET_MEDIA_INPUT_ACTION_PLAY : ObsMediaInputAction.OBS_WEBSOCKET_MEDIA_INPUT_ACTION_PAUSE).ToString() })],
                    ReadResponses = _ => play ? ObsMediaState.Playing : ObsMediaState.Paused,
                    IsConfirmed = result => input.IsRemoved || this.State == (ObsMediaState)result!,
                    Refresh = [new RefreshTarget(ReadKind.MediaStatus, input.Uuid)],
                },
                cancellationToken);
        }

        /// <summary>
        /// Goes to the next item of the playlist of the media input, and returns when OBS Studio has answered.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when OBS Studio has answered.</returns>
        /// <exception cref="ObsRemovedException">The input was removed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        /// <remarks>
        /// Only an input that has a playlist (the VLC video source) does anything: a media source plays one file, and OBS Studio 33.0.0 answers the call and does nothing, with no event. What
        /// the playlist does to <see cref="State"/> is read when OBS Studio says that it went to the next item, and the call does not wait for it.
        /// </remarks>
        public Task NextAsync(CancellationToken cancellationToken = default)
        {
            return this.TriggerAsync(ObsMediaInputAction.OBS_WEBSOCKET_MEDIA_INPUT_ACTION_NEXT, "going to the next item of", cancellationToken);
        }

        /// <summary>
        /// Goes to the previous item of the playlist of the media input, and returns when OBS Studio has answered.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when OBS Studio has answered.</returns>
        /// <exception cref="ObsRemovedException">The input was removed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        /// <remarks>See <see cref="NextAsync"/>: it is the same, backwards.</remarks>
        public Task PreviousAsync(CancellationToken cancellationToken = default)
        {
            return this.TriggerAsync(ObsMediaInputAction.OBS_WEBSOCKET_MEDIA_INPUT_ACTION_PREVIOUS, "going to the previous item of", cancellationToken);
        }

        /// <summary>
        /// Moves the media to a position, and returns when OBS Studio has answered.
        /// </summary>
        /// <param name="position">The position, from the beginning of the media, in whole milliseconds (a part of a millisecond is rounded). OBS Studio does not check it against the length: a position beyond the end ends the media.</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when OBS Studio has answered.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="position"/> is negative, or more than about 24 days.</exception>
        /// <exception cref="ObsRemovedException">The input was removed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change: the media neither plays nor is paused (<see cref="ObsResponseException.ErrorCode"/> is <see cref="RequestStatusCode.InvalidResourceState"/>).</exception>
        /// <remarks>
        /// The position is not in the model, so there is nothing to wait for: ask <see cref="GetStatusAsync"/> where the media is. What OBS Studio does with it (measured with OBS Studio 33.0.0): a media that plays
        /// and is shown goes to the position and plays on from there, and a position beyond the end ends the media (<see cref="ObsMediaState.Ended"/>). A media that is not shown takes the change and does not move,
        /// and one that is paused takes it and then says position zero and plays from the start (the position of a paused media is not kept). See <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes.
        /// </remarks>
        public Task SeekAsync(TimeSpan position, CancellationToken cancellationToken = default)
        {
            var milliseconds = ToMilliseconds(position, nameof(position), allowNegative: false);
            var input = this.Input;
            return input.Model.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = $"moving the media of {input.Label} to {milliseconds} ms",
                    Subject = input.Label,
                    SubjectGone = () => input.IsRemoved,
                    Check = this.CheckWritable,
                    BuildRequests = () => [new WriteRequest(RequestType.SetMediaInputCursor, new { inputUuid = input.Uuid, mediaCursor = milliseconds })],
                },
                cancellationToken);
        }

        /// <summary>
        /// Moves the media forwards or backwards from where it is, and returns when OBS Studio has answered.
        /// </summary>
        /// <param name="offset">How far to move, in whole milliseconds (a part of a millisecond is rounded): positive goes forwards, negative backwards. OBS Studio stops at the beginning, and does not check against the end.</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when OBS Studio has answered.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="offset"/> is more than about 24 days either way.</exception>
        /// <exception cref="ObsRemovedException">The input was removed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change: the media neither plays nor is paused (<see cref="ObsResponseException.ErrorCode"/> is <see cref="RequestStatusCode.InvalidResourceState"/>).</exception>
        /// <remarks>See <see cref="SeekAsync"/>.</remarks>
        public Task SkipAsync(TimeSpan offset, CancellationToken cancellationToken = default)
        {
            var milliseconds = ToMilliseconds(offset, nameof(offset), allowNegative: true);
            var input = this.Input;
            return input.Model.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = $"moving the media of {input.Label} by {milliseconds} ms",
                    Subject = input.Label,
                    SubjectGone = () => input.IsRemoved,
                    Check = this.CheckWritable,
                    BuildRequests = () => [new WriteRequest(RequestType.OffsetMediaInputCursor, new { inputUuid = input.Uuid, mediaCursorOffset = milliseconds })],
                },
                cancellationToken);
        }

        /// <summary>
        /// Asks OBS Studio what the media is doing, how long it is, and where it is.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A request that was not sent yet is not sent.</param>
        /// <returns>What OBS Studio says at this moment.</returns>
        /// <exception cref="ObsRemovedException">The input was destroyed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call.</exception>
        /// <exception cref="TimeoutException">OBS Studio did not answer in time (<see cref="ObsModelOptions.WriteTimeout"/>).</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the request.</exception>
        /// <remarks>
        /// The length and the position are only known while the media plays or is paused, and the position moves all the time: it is not kept in the model. The answer is not applied to
        /// <see cref="State"/>: it is what OBS Studio holds at the moment of the answer, including the states that the events do not announce (opening, buffering, an error). The request is made in its turn,
        /// after the changes that were asked for before it.
        /// </remarks>
        public Task<ObsMediaStatus> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            var input = this.Input;
            return input.Model.Writer.ExecuteAsync<ObsMediaStatus>(
                new WriteJob
                {
                    Description = $"asking for the status of the media of {input.Label}",
                    Subject = input.Label,
                    ReadOnly = true,
                    Check = input.CheckNotDestroyed,
                    BuildRequests = () => [new WriteRequest(RequestType.GetMediaInputStatus, new { inputUuid = input.Uuid })],
                    ReadResponses = static responses =>
                    {
                        var status = (MediaInputStatusResponse)responses[0].ResponseData!;
                        return new ObsMediaStatus(
                            ModelEnums.ToModel(status.MediaState),
                            status.MediaDuration is { } duration ? TimeSpan.FromMilliseconds(duration) : null,
                            status.MediaCursor is { } cursor ? TimeSpan.FromMilliseconds(cursor) : null);
                    },
                },
                cancellationToken);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"Media of {this.Input.Label}: {this.State}";
        }

        internal bool SetState(ObsMediaState state)
        {
            if (Volatile.Read(ref this._state) == (int)state)
            {
                return false;
            }

            Volatile.Write(ref this._state, (int)state);
            return true;
        }

        internal void RaisePropertyChanged(string propertyName)
        {
            this.Input.Model.Raise(this.PropertyChanged, this, propertyName);
        }

        // OBS Studio takes the position as a whole number of milliseconds, as a signed 32 bit integer.
        private static int ToMilliseconds(TimeSpan value, string name, bool allowNegative)
        {
            var milliseconds = Math.Round(value.TotalMilliseconds);
            if (milliseconds > int.MaxValue || milliseconds < (allowNegative ? int.MinValue : 0))
            {
                throw new ArgumentOutOfRangeException(name, value, allowNegative ? "The offset is at most about 24 days, forwards or backwards." : "The position is from 0 to about 24 days.");
            }

            return (int)milliseconds;
        }

        // An input that was removed does not take changes (a scene or an input that OBS Studio keeps alive can only be removed for good).
        private void CheckWritable()
        {
            var input = this.Input;
            if (input.State != ObsObjectState.Live)
            {
                throw new ObsRemovedException(input.Label, input.State);
            }
        }

        // An action that makes the media do something that the model shows: the call ends when it shows it.
        private Task ActAsync(ObsMediaInputAction action, ObsMediaState shown, string doing, CancellationToken cancellationToken)
        {
            var input = this.Input;
            return input.Model.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = $"{doing} the media of {input.Label}",
                    Subject = input.Label,
                    SubjectGone = () => input.IsRemoved,
                    Check = this.CheckWritable,
                    BuildRequests = () => [new WriteRequest(RequestType.TriggerMediaInputAction, new { inputUuid = input.Uuid, mediaAction = action.ToString() })],
                    IsConfirmed = _ => input.IsRemoved || this.State == shown,
                    Refresh = [new RefreshTarget(ReadKind.MediaStatus, input.Uuid)],
                },
                cancellationToken);
        }

        // An action that the model does not show the result of: the call ends when OBS Studio has answered.
        private Task TriggerAsync(ObsMediaInputAction action, string doing, CancellationToken cancellationToken)
        {
            var input = this.Input;
            return input.Model.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = $"{doing} the media of {input.Label}",
                    Subject = input.Label,
                    SubjectGone = () => input.IsRemoved,
                    Check = this.CheckWritable,
                    BuildRequests = () => [new WriteRequest(RequestType.TriggerMediaInputAction, new { inputUuid = input.Uuid, mediaAction = action.ToString() })],
                },
                cancellationToken);
        }
    }
}
