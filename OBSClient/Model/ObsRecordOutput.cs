namespace OBSStudioClient.Model
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Events;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Model.Engine;
    using OBSStudioClient.Responses;

    /// <summary>
    /// The recording of OBS Studio. Besides what every output has, it can be paused and split, and it says which file it writes.
    /// </summary>
    public sealed class ObsRecordOutput : ObsOutput
    {
        private string? _currentFile;

        internal ObsRecordOutput(ObsModel model)
            : base(model, ObsOutputKind.Record)
        {
        }

        /// <summary>
        /// Occurs when the recording continues in a new file: OBS Studio has split the recording (because of its settings, or <see cref="SplitFileAsync"/>) and has begun to write another file.
        /// The event carries the path of the new file, which is <see cref="CurrentFile"/> from then on.
        /// </summary>
        /// <remarks>
        /// It is raised in the order in which the events of OBS Studio arrived, one at a time, on <see cref="ObsModelOptions.NotificationContext"/> (or a thread of the thread pool). The same
        /// event is available as <c>Model.Client.RecordFileChanged</c>, which the client raises from a dispatcher of its own.
        /// </remarks>
        public event EventHandler<RecordFileChangedEventArgs>? FileChanged;

        /// <summary>
        /// Gets whether the recording is paused: <see cref="ObsOutput.Status"/> is <see cref="ObsOutputStatus.Paused"/>.
        /// </summary>
        public bool IsPaused => this.Status == ObsOutputStatus.Paused;

        /// <summary>
        /// Gets the path of the file of the recording: the file that OBS Studio is writing, or wrote last. It is null until the model has seen OBS Studio say one.
        /// </summary>
        /// <remarks>
        /// <para>
        /// OBS Studio says the file in no request, only in events: when the recording has started (obs-websocket 5.5.0 and newer; older ones say it when the recording stops), when it goes on in a new
        /// file (<see cref="FileChanged"/>), and when it has stopped. So the model knows the file of a recording that it saw start or stop, and not of one that was under way when it connected, until
        /// that one is split or stopped. The value is kept when the connection is lost (like everything that the model holds, it is the last that was known), and it is not forgotten when the next
        /// recording starts: it is the file of the recording that is under way as soon as OBS Studio says that one. A handler of the change of <see cref="ObsOutput.Status"/> finds the new file
        /// set. When the file changes, <see cref="ObsModel.Changed"/> and PropertyChanged tell, the property before the <see cref="FileChanged"/> event.
        /// </para>
        /// <para>OBS Studio writes the path with forward slashes on every platform (<c>C:/Videos/2026-11-10 21-59-29.mp4</c>); the model hands it on as it is.</para>
        /// </remarks>
        public string? CurrentFile => Volatile.Read(ref this._currentFile);

        // Brings the object in line with the model. Returns whether the file changed.
        internal bool SetCurrentFile(string? file, out string? previous)
        {
            previous = Volatile.Read(ref this._currentFile);
            if (string.Equals(previous, file, StringComparison.Ordinal))
            {
                return false;
            }

            Volatile.Write(ref this._currentFile, file);
            return true;
        }

        internal void RaiseFileChanged(RecordFileChangedEventArgs args)
        {
            this.Model.RaiseEvent(this.FileChanged, args, nameof(this.FileChanged), this);
        }

        /// <summary>
        /// Asks OBS Studio for the figures of the recording: how long it runs (the timecode and the duration in milliseconds) and how many bytes it has written.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A request that was not sent yet is not sent.</param>
        /// <returns>
        /// The figures at the moment that OBS Studio answered. <see cref="RecordStatusResponse.OutputActive"/> and <see cref="RecordStatusResponse.OutputPaused"/> are the state at that moment too;
        /// <see cref="ObsOutput.Status"/> is the one that the model keeps current.
        /// </returns>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call.</exception>
        /// <exception cref="TimeoutException">OBS Studio did not answer in time (<see cref="ObsModelOptions.WriteTimeout"/>).</exception>
        /// <remarks>
        /// The figures change all the time, and OBS Studio sends no event for them: the model does not keep them, ask when you need them. The request is made in its turn, after the changes
        /// that were asked for before it (see <see cref="ObsSceneItem.SetEnabledAsync"/>). The figures of a recording that does not run are zero.
        /// </remarks>
        public Task<RecordStatusResponse> GetStatisticsAsync(CancellationToken cancellationToken = default)
        {
            return this.Model.Writer.ExecuteAsync<RecordStatusResponse>(
                new WriteJob
                {
                    Description = "asking for the figures of the recording",
                    ReadOnly = true,
                    BuildRequests = () => [new WriteRequest(RequestType.GetRecordStatus, null)],
                    ReadResponses = static responses => (RecordStatusResponse)responses[0].ResponseData!,
                },
                cancellationToken);
        }

        /// <summary>
        /// Pauses the recording when it is not paused, and resumes it when it is, and returns when the model shows it.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes with the value that <see cref="IsPaused"/> has when the call ends.</returns>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the change, and the model does not show it. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        /// <remarks>
        /// <para>
        /// OBS Studio decides by itself what to do, so two toggles that are asked for one after the other make two toggles. As with <see cref="PauseAsync"/>, OBS Studio can answer success and not pause
        /// (a recording that cannot be paused, or that does not run): the model is read again then, and the result is what <see cref="IsPaused"/> shows.
        /// </para>
        /// <para>See <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes and the wait.</para>
        /// </remarks>
        public Task<bool> TogglePauseAsync(CancellationToken cancellationToken = default)
        {
            return this.Model.Writer.ExecuteAsync<bool>(
                new WriteJob
                {
                    Description = "toggling the pause of the recording",
                    BuildRequests = () => [new WriteRequest(RequestType.ToggleRecordPause, null)],
                    ReadResponses = static responses => ((OutputPausedResponse)responses[0].ResponseData!).OutputPaused,
                    IsConfirmed = result => this.IsPaused == (bool)result!,
                    Finish = _ => this.IsPaused,
                    Refresh = [new RefreshTarget(ReadKind.Record)],
                },
                cancellationToken);
        }

        /// <summary>
        /// Splits the recording: OBS Studio finishes the file that it is writing and goes on in a new one. It returns when OBS Studio has accepted the request.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when OBS Studio has answered.</returns>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the request: the recording is not running, or it does not allow splitting (<c>Verify that file splitting is enabled in the output settings</c>), or this OBS Studio does not have the request (it needs obs-websocket 5.5.0).</exception>
        /// <remarks>
        /// OBS Studio writes the new file a moment after it has answered, and says so in the event that <see cref="FileChanged"/> raises. Splitting changes nothing in the model, so the call does not wait for it.
        /// See <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes.
        /// </remarks>
        public Task SplitFileAsync(CancellationToken cancellationToken = default)
        {
            return this.Model.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = "splitting the recording file",
                    BuildRequests = () => [new WriteRequest(RequestType.SplitRecordFile, null)],
                },
                cancellationToken);
        }

        /// <summary>
        /// Adds a chapter marker to the file that is being recorded, and returns when OBS Studio has accepted the request.
        /// </summary>
        /// <param name="name">The name of the chapter, or null to let OBS Studio name it.</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when OBS Studio has answered.</returns>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the request: the recording is not running, or its format has no chapters (<c>Verify that the output being used supports chapter markers</c>), or this OBS Studio does not have the request (it needs obs-websocket 5.5.0).</exception>
        /// <remarks>
        /// As of OBS Studio 30.2 only the Hybrid MP4 format has chapters. Adding a chapter changes nothing in the model, so the call does not wait for it. See
        /// <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes.
        /// </remarks>
        public Task CreateChapterAsync(string? name = null, CancellationToken cancellationToken = default)
        {
            return this.Model.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = name is null ? "adding a chapter to the recording" : $"adding the chapter '{name}' to the recording",
                    BuildRequests = () => [new WriteRequest(RequestType.CreateRecordChapter, name is null ? null : new { chapterName = name })],
                },
                cancellationToken);
        }

        /// <summary>
        /// Pauses the recording, and returns when the model shows it.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when <see cref="IsPaused"/> is true, or when the model has been read again after the events did not show it (see the remarks).</returns>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the change, and the model does not show it. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        /// <remarks>
        /// <para>
        /// OBS Studio can answer success and not pause: a recording that uses the encoder of the stream (the quality "same as the stream" of the simple output) cannot be paused, and a recording
        /// that does not run is not refused either. The model is read again then, and the call returns with <see cref="IsPaused"/> false (and <see cref="ResumeAsync"/> would be refused): look at
        /// <see cref="IsPaused"/> after the call.
        /// </para>
        /// <para>See <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes and the wait.</para>
        /// </remarks>
        public Task PauseAsync(CancellationToken cancellationToken = default)
        {
            return this.Model.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = "pausing the recording",
                    BuildRequests = () => [new WriteRequest(RequestType.PauseRecord, null)],
                    IsConfirmed = _ => this.IsPaused,
                    Refresh = [new RefreshTarget(ReadKind.Record)],
                },
                cancellationToken);
        }

        /// <summary>
        /// Resumes the recording after it was paused, and returns when the model shows it.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when <see cref="IsPaused"/> is false, or when the model has been read again after the events did not show it.</returns>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the change, and the model does not show it. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change: the recording is not paused.</exception>
        /// <remarks>See <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes and the wait.</remarks>
        public Task ResumeAsync(CancellationToken cancellationToken = default)
        {
            return this.Model.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = "resuming the recording",
                    BuildRequests = () => [new WriteRequest(RequestType.ResumeRecord, null)],
                    IsConfirmed = _ => !this.IsPaused,
                    Refresh = [new RefreshTarget(ReadKind.Record)],
                },
                cancellationToken);
        }
    }
}
