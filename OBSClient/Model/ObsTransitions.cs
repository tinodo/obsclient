namespace OBSStudioClient.Model
{
    using System.ComponentModel;
    using System.Text.Json;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Events;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Messages;
    using OBSStudioClient.Model.Engine;
    using OBSStudioClient.Responses;

    /// <summary>
    /// The scene transitions of OBS Studio: which one is current, how long it takes, whether one is running, and the calls that change them and trigger the transition of studio mode.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is the same object for as long as the <see cref="ObsModel"/> lives (<see cref="ObsModel.Transitions"/>), and says with <see cref="INotifyPropertyChanged"/> when <see cref="Current"/>,
    /// <see cref="Duration"/> and <see cref="IsRunning"/> change. The list of the transitions is not kept: OBS Studio does not announce that one was added or removed, so
    /// <see cref="GetAllAsync"/> asks.
    /// </para>
    /// <para>
    /// What OBS Studio does (measured with OBS Studio 33.0.0): a scene that is switched to is on the program output at once as far as a question about it goes, and the event that says so comes when
    /// the transition has ended; the model reads the program scene when a transition starts, so <see cref="ObsModel.ProgramScene"/> shows the scene that is being switched to from the start, and
    /// <see cref="IsRunning"/> says that it is not on the air yet. A scene can bring a transition of its own (<see cref="ObsScene.GetTransitionOverrideAsync"/>), which is used for it without
    /// <see cref="Current"/> changing.
    /// </para>
    /// </remarks>
    public sealed class ObsTransitions : INotifyPropertyChanged
    {
        // The duration that OBS Studio takes for a transition (SetCurrentSceneTransitionDuration, and the override of a scene).
        private static readonly TimeSpan ShortestDuration = TimeSpan.FromMilliseconds(50);
        private static readonly TimeSpan LongestDuration = TimeSpan.FromMilliseconds(20000);

        private readonly ObsModel _model;
        private ObsTransition? _current;
        private long _durationMilliseconds = -1;
        private int _running;
        private long _starts;

        internal ObsTransitions(ObsModel model)
        {
            this._model = model;
        }

        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Occurs when a transition starts. It is raised in the order in which the events of OBS Studio arrived, one at a time, on <see cref="ObsModelOptions.NotificationContext"/> (or a thread
        /// of the thread pool). The same event is available as <c>Model.Client.SceneTransitionStarted</c>, which the client raises from a dispatcher of its own.
        /// </summary>
        public event EventHandler<TransitionNameEventArgs>? Started;

        /// <summary>
        /// Occurs when a transition has ended; for a transition that plays a video (a stinger) that is its cut point, and the video goes on until <see cref="VideoEnded"/>. It is raised like
        /// <see cref="Started"/>.
        /// </summary>
        public event EventHandler<TransitionNameEventArgs>? Ended;

        /// <summary>
        /// Occurs when the video of a transition has ended, which is the end of the transition (OBS Studio says it for every transition). It is raised like <see cref="Started"/>.
        /// </summary>
        public event EventHandler<TransitionNameEventArgs>? VideoEnded;

        /// <summary>
        /// Gets the current transition: the one that is used when a scene is switched to (the scene can bring another one, see <see cref="ObsScene.GetTransitionOverrideAsync"/>). It is null until
        /// it is known, and when the scene collection has no transition.
        /// </summary>
        public ObsTransition? Current => Volatile.Read(ref this._current);

        /// <summary>
        /// Gets how long a transition takes, for the transitions that take time (see <see cref="ObsTransition.IsFixed"/>). It is the same for all of them, and null until OBS Studio has said: it
        /// does not say while the current transition is a cut, until it is told or the duration is changed.
        /// </summary>
        public TimeSpan? Duration
        {
            get
            {
                var milliseconds = Volatile.Read(ref this._durationMilliseconds);
                return milliseconds < 0 ? null : TimeSpan.FromMilliseconds(milliseconds);
            }
        }

        /// <summary>
        /// Gets whether a transition is running: from the moment that OBS Studio says that it started until it says that its video ended. A transition that was already running when the model was
        /// connected is not known to run. While it is true the scene that was switched to is the one that <see cref="ObsModel.ProgramScene"/> shows, and is not on the air yet.
        /// </summary>
        public bool IsRunning => Volatile.Read(ref this._running) != 0;

        // How many transitions the events of OBS Studio said started: a change that triggers one is done when the number has gone up (a cut is over when the model is asked).
        internal long Starts => Interlocked.Read(ref this._starts);

        /// <summary>
        /// Asks OBS Studio for its scene transitions.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A request that was not sent yet is not sent.</param>
        /// <returns>The transitions, as OBS Studio lists them, at the moment that it answered.</returns>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call.</exception>
        /// <exception cref="TimeoutException">OBS Studio did not answer in time (<see cref="ObsModelOptions.WriteTimeout"/>).</exception>
        /// <remarks>
        /// OBS Studio has a cut and a fade to begin with, and the user can add more in its window (a swipe, a slide, a stinger, a fade to a color, a luma wipe). The list is not kept in the model, because OBS
        /// Studio does not announce a change of it; <see cref="Current"/> is. The request is made in its turn, after the changes that were asked for before it (see <see cref="ObsSceneItem.SetEnabledAsync"/>).
        /// </remarks>
        public Task<IReadOnlyList<ObsTransition>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return this._model.Writer.ExecuteAsync<IReadOnlyList<ObsTransition>>(
                new WriteJob
                {
                    Description = "asking for the scene transitions of OBS Studio",
                    ReadOnly = true,
                    BuildRequests = () => [new WriteRequest(RequestType.GetSceneTransitionList, null)],
                    ReadResponses = static responses => ((SceneTransitionListResponse)responses[0].ResponseData!).Transitions
                        .Select(transition => new ObsTransition(transition.TransitionName, transition.TransitionUuid, transition.TransitionKind, transition.TransitionFixed, transition.TransitionConfigurable))
                        .ToArray(),
                },
                cancellationToken);
        }

        /// <summary>
        /// Asks OBS Studio for the kinds of transition that it has.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A request that was not sent yet is not sent.</param>
        /// <returns>The kinds (<c>cut_transition</c>, <c>fade_transition</c>, ...).</returns>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call.</exception>
        /// <exception cref="TimeoutException">OBS Studio did not answer in time (<see cref="ObsModelOptions.WriteTimeout"/>).</exception>
        /// <remarks>
        /// obs-websocket can not make a transition: the kinds say what the user can add in OBS Studio. The request is made in its turn, after the changes that were asked for before it.
        /// </remarks>
        public Task<IReadOnlyList<string>> GetKindsAsync(CancellationToken cancellationToken = default)
        {
            return this._model.Writer.ExecuteAsync<IReadOnlyList<string>>(
                new WriteJob
                {
                    Description = "asking for the kinds of scene transitions of OBS Studio",
                    ReadOnly = true,
                    BuildRequests = () => [new WriteRequest(RequestType.GetTransitionKindList, null)],
                    ReadResponses = static responses => ((TransitionKindsResponse)responses[0].ResponseData!).TransitionKinds,
                },
                cancellationToken);
        }

        /// <summary>
        /// Makes a transition the current one, and returns when the model shows it.
        /// </summary>
        /// <param name="name">The name of the transition (<see cref="GetAllAsync"/> lists them).</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when <see cref="Current"/> is the transition.</returns>
        /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the change, and the model does not show it: the events did not show it in time, and the model could not be read again in time. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change: it has no transition of that name (<see cref="ObsResponseException.ErrorCode"/> is <see cref="RequestStatusCode.ResourceNotFound"/>).</exception>
        /// <remarks>
        /// OBS Studio sends the event again when the transition is the current one already (measured with OBS Studio 33.0.0), so the model is read again and nothing changes. See
        /// <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes and the wait.
        /// </remarks>
        public Task SetCurrentAsync(string name, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            return this._model.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = $"making '{name}' the current scene transition",
                    Subject = $"scene transition '{name}'",
                    BuildRequests = () => [new WriteRequest(RequestType.SetCurrentSceneTransition, new { transitionName = name })],
                    IsConfirmed = _ => this.Current is { } current && string.Equals(current.Name, name, StringComparison.Ordinal),
                    Refresh = [new RefreshTarget(ReadKind.Transition)],
                    Map = static (_, response) => response.RequestStatus.Code == RequestStatusCode.ResourceNotFound ? new ObsResponseException(response.RequestStatus) : null,
                },
                cancellationToken);
        }

        /// <summary>
        /// Sets how long the transitions take, and returns when the model shows it.
        /// </summary>
        /// <param name="duration">The duration, from 50 ms to 20 seconds (a part of a millisecond is rounded).</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when <see cref="Duration"/> is the duration.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="duration"/> is less than 50 ms or more than 20 seconds. Nothing was sent.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the change, and the model does not show it: the events did not show it in time. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        /// <remarks>
        /// The duration is the same for all the transitions that have one, and OBS Studio takes it while the current transition is a cut too (it uses it when another one becomes the current one). It sends
        /// an event only when the duration changed. See <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes and the wait.
        /// </remarks>
        public Task SetDurationAsync(TimeSpan duration, CancellationToken cancellationToken = default)
        {
            var milliseconds = ToMilliseconds(duration, nameof(duration));
            return this._model.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = $"setting the duration of the scene transitions to {milliseconds} ms",
                    BuildRequests = () => [new WriteRequest(RequestType.SetCurrentSceneTransitionDuration, new { transitionDuration = milliseconds })],
                    IsConfirmed = _ => Volatile.Read(ref this._durationMilliseconds) == milliseconds,
                },
                cancellationToken);
        }

        /// <summary>
        /// Asks OBS Studio for the settings of the current transition.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A request that was not sent yet is not sent.</param>
        /// <returns>The settings, as a JSON object: empty for a transition that has none (see <see cref="ObsTransition.IsConfigurable"/>).</returns>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call.</exception>
        /// <exception cref="TimeoutException">OBS Studio did not answer in time (<see cref="ObsModelOptions.WriteTimeout"/>).</exception>
        /// <remarks>
        /// OBS Studio does not announce a change of the settings, so they are not kept in the model: this asks. What the fields are called depends on the kind of the transition.
        /// </remarks>
        public Task<JsonElement> GetSettingsAsync(CancellationToken cancellationToken = default)
        {
            return this._model.Writer.ExecuteAsync<JsonElement>(
                new WriteJob
                {
                    Description = "asking for the settings of the current scene transition",
                    ReadOnly = true,
                    BuildRequests = () => [new WriteRequest(RequestType.GetCurrentSceneTransition, null)],
                    ReadResponses = static responses => SettingsValue.From(((TransitionResponse)responses[0].ResponseData!).TransitionSettings).Element,
                },
                cancellationToken);
        }

        /// <summary>
        /// Changes the settings of the current transition, and returns when OBS Studio has made the change.
        /// </summary>
        /// <param name="settings">The settings to change: an object (for example <c>new { direction = "left" }</c>), a dictionary, or a <see cref="JsonElement"/>. What the fields are called depends on the kind of the transition.</param>
        /// <param name="overlay">True to change the settings that are given and leave the others as they are; false to take all the settings back to their defaults first.</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when OBS Studio has answered that it made the change.</returns>
        /// <exception cref="ArgumentException"><paramref name="settings"/> is not an object, or can not be written as JSON.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change: the current transition has no settings (<see cref="ObsResponseException.ErrorCode"/> is <see cref="RequestStatusCode.ResourceNotConfigurable"/>; measured for a cut and a fade).</exception>
        /// <remarks>
        /// Nothing in the model shows the settings, so the call has nothing to wait for but the answer of OBS Studio: ask what it holds with <see cref="GetSettingsAsync"/>. OBS Studio does not check the
        /// settings (see <see cref="ObsInput.SetSettingsAsync"/>).
        /// </remarks>
        public Task SetSettingsAsync(object settings, bool overlay = true, CancellationToken cancellationToken = default)
        {
            var given = ModelJson.AsObject(settings, nameof(settings), "the settings of a scene transition", "new { direction = \"left\" }");
            return this._model.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = "changing the settings of the current scene transition",
                    BuildRequests = () => [new WriteRequest(RequestType.SetCurrentSceneTransitionSettings, new { transitionSettings = given, overlay })],
                },
                cancellationToken);
        }

        /// <summary>
        /// Asks OBS Studio how far the transition that runs has come.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A request that was not sent yet is not sent.</param>
        /// <returns>The position of the transition, from 0.0 to 1.0: 1.0 when none runs.</returns>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call.</exception>
        /// <exception cref="TimeoutException">OBS Studio did not answer in time (<see cref="ObsModelOptions.WriteTimeout"/>).</exception>
        /// <remarks>
        /// The position moves all the time, and OBS Studio sends no event for it, so it is not kept in the model; <see cref="IsRunning"/> is.
        /// </remarks>
        public Task<double> GetProgressAsync(CancellationToken cancellationToken = default)
        {
            return this._model.Writer.ExecuteAsync<double>(
                new WriteJob
                {
                    Description = "asking for the position of the scene transition",
                    ReadOnly = true,
                    BuildRequests = () => [new WriteRequest(RequestType.GetCurrentSceneTransitionCursor, null)],
                    ReadResponses = static responses => (double)((TransitionCursorResponse)responses[0].ResponseData!).TransitionCursor,
                },
                cancellationToken);
        }

        /// <summary>
        /// Triggers the transition of studio mode (the Transition button): the preview scene becomes the program scene. It returns when the model shows that a transition runs.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when a transition has started since the call was made, or runs already, or when the model has been read again after the events did not show it.</returns>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio answered, and the model does not show that a transition started: the events did not show it in time, and the model could not be read again in time. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change: studio mode is off (<see cref="ObsResponseException.ErrorCode"/> is <see cref="RequestStatusCode.StudioModeNotActive"/>).</exception>
        /// <remarks>
        /// <para>
        /// The call returns when the transition has started, not when it has ended: <see cref="IsRunning"/> and <see cref="VideoEnded"/> say when it ends. Measured with OBS Studio 33.0.0: the program
        /// scene is the old preview scene from the start (<see cref="ObsModel.ProgramScene"/> shows it, as soon as the model has read it), and the preview scene becomes the old program scene
        /// when the transition has ended. OBS Studio does nothing when a transition runs already: the call returns at once then. It makes a transition from the preview scene when that is the program scene too.
        /// </para>
        /// <para>
        /// See <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes and the wait.
        /// </para>
        /// </remarks>
        public Task TriggerAsync(CancellationToken cancellationToken = default)
        {
            var startsBefore = 0L;
            return this._model.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = "triggering the transition of studio mode",
                    BuildRequests = () =>
                    {
                        startsBefore = this.Starts;
                        return [new WriteRequest(RequestType.TriggerStudioModeTransition, null)];
                    },
                    IsConfirmed = _ => this.IsRunning || this.Starts > startsBefore,
                    Refresh = [new RefreshTarget(ReadKind.Program)],
                },
                cancellationToken);
        }

        /// <summary>
        /// Sets the position of the T-bar of studio mode, which moves a transition by hand. It returns when OBS Studio has answered.
        /// </summary>
        /// <param name="position">The position, from 0.0 to 1.0.</param>
        /// <param name="release">Whether to release the T-bar. Pass false only when another position follows.</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when OBS Studio has answered that it made the change.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="position"/> is not from 0.0 to 1.0. Nothing was sent.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change: studio mode is off (<see cref="ObsResponseException.ErrorCode"/> is <see cref="RequestStatusCode.StudioModeNotActive"/>).</exception>
        /// <remarks>
        /// <para>
        /// obs-websocket says that this request will be replaced in a future version. The model shows what it does through the events like anything else (the transition starts when the T-bar moves,
        /// and the program scene changes when it is released at the end), and the call has nothing to wait for but the answer of OBS Studio. What the T-bar does is OBS Studio's: with OBS Studio 33.0.0 a
        /// T-bar that is moved to 0.5 and not released holds the transition there, and one that is moved to 1.0 and released ends it, but the scenes that are the program scene and the preview scene
        /// afterwards were not those of a trigger (the T-bar has a direction of its own). Use <see cref="TriggerAsync"/> to switch from the preview scene to the program scene.
        /// </para>
        /// </remarks>
        public Task SetTBarPositionAsync(double position, bool release = true, CancellationToken cancellationToken = default)
        {
            if (double.IsNaN(position) || position < 0.0 || position > 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(position), position, "The position of the T-bar is from 0.0 to 1.0.");
            }

            return this._model.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = $"setting the T-bar to {position:0.###}",
                    BuildRequests = () => [new WriteRequest(RequestType.SetTBarPosition, new { position, release })],
                },
                cancellationToken);
        }

        /// <summary>
        /// Gets the whole milliseconds of a duration that OBS Studio takes (50 ms to 20 seconds), or throws.
        /// </summary>
        /// <param name="duration">The duration.</param>
        /// <param name="name">The name of the argument.</param>
        /// <returns>The milliseconds.</returns>
        internal static int ToMilliseconds(TimeSpan duration, string name)
        {
            var milliseconds = Math.Round(duration.TotalMilliseconds);
            if (milliseconds < ShortestDuration.TotalMilliseconds || milliseconds > LongestDuration.TotalMilliseconds)
            {
                throw new ArgumentOutOfRangeException(name, duration, "A scene transition takes from 50 milliseconds to 20 seconds.");
            }

            return (int)milliseconds;
        }

        internal bool SetCurrent(TransitionInfo? info, out ObsTransition? before)
        {
            before = this._current;
            if (before is null ? info is null : info is not null && SameAs(before, info))
            {
                return false;
            }

            Volatile.Write(ref this._current, info is null ? null : new ObsTransition(info.Name, info.Uuid, info.Kind, info.IsFixed, info.IsConfigurable));
            return true;
        }

        internal bool SetDuration(int? milliseconds, out TimeSpan? before)
        {
            before = this.Duration;
            var value = milliseconds ?? -1;
            if (Volatile.Read(ref this._durationMilliseconds) == value)
            {
                return false;
            }

            Volatile.Write(ref this._durationMilliseconds, value);
            return true;
        }

        internal bool SetRunning(bool running, out bool before)
        {
            before = this.IsRunning;
            if (before == running)
            {
                return false;
            }

            Volatile.Write(ref this._running, running ? 1 : 0);
            return true;
        }

        internal void SetStarts(long starts)
        {
            Interlocked.Exchange(ref this._starts, starts);
        }

        internal void RaisePropertyChanged(string propertyName)
        {
            this._model.Raise(this.PropertyChanged, this, propertyName);
        }

        internal void RaiseStarted(TransitionNameEventArgs args)
        {
            this._model.RaiseEvent(this.Started, args, nameof(this.Started), this);
        }

        internal void RaiseEnded(TransitionNameEventArgs args)
        {
            this._model.RaiseEvent(this.Ended, args, nameof(this.Ended), this);
        }

        internal void RaiseVideoEnded(TransitionNameEventArgs args)
        {
            this._model.RaiseEvent(this.VideoEnded, args, nameof(this.VideoEnded), this);
        }

        private static bool SameAs(ObsTransition shown, TransitionInfo info)
        {
            return shown.Uuid == info.Uuid
                && shown.IsFixed == info.IsFixed
                && shown.IsConfigurable == info.IsConfigurable
                && string.Equals(shown.Name, info.Name, StringComparison.Ordinal)
                && string.Equals(shown.Kind, info.Kind, StringComparison.Ordinal);
        }
    }
}
