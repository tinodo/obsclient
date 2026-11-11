namespace OBSStudioClient.Model
{
    using System.ComponentModel;
    using System.Threading.Channels;
    using Microsoft.Extensions.Logging;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Messages;
    using OBSStudioClient.Responses;
    using OBSStudioClient.Model.Engine;

    /// <summary>
    /// Runs the model of an <see cref="ObsModel"/>: the one loop that takes what arrives (the events and the start and end of connections, each
    /// with the number that the receive loop of the <see cref="ObsClient"/> gave it on arrival, and the results of the reads that it asked for), gives
    /// it to the <see cref="ModelEngine"/> one item at a time, sends the reads that the engine wants, and publishes the outcome.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nothing that belongs to the application runs on this loop: not a handler, not a callback. The notifications are queued, and raised by the
    /// <see cref="ModelNotifier"/>. The receive loop of the client only puts events in the inbox, which never blocks, so a slow application cannot hold up
    /// the reading of OBS Studio, or the model.
    /// </para>
    /// <para>
    /// Reads are sent one at a time. What arrives while a read is under way is processed meanwhile, in the order of arrival; the engine knows
    /// from the numbers which events are newer than a read.
    /// </para>
    /// </remarks>
    internal sealed class ModelHost
    {
        /// <summary>The events that the model needs. They are always subscribed to, with the ones that the application asks for.</summary>
        public const EventSubscriptions RequiredEvents =
            EventSubscriptions.General | EventSubscriptions.Config | EventSubscriptions.Scenes | EventSubscriptions.Inputs | EventSubscriptions.SceneItems | EventSubscriptions.Ui | EventSubscriptions.Outputs | EventSubscriptions.Filters | EventSubscriptions.MediaInputs;

        /// <summary>The oldest obs-websocket that the model works with: the first one that identifies scenes and inputs by UUID (OBS Studio 30.1).</summary>
        public static readonly Version RequiredWebSocketVersion = new(5, 4, 0);

        private static readonly TimeSpan VersionRetryDelay = TimeSpan.FromSeconds(1);

        // When the engine wants a read that it does not hand out (it should not happen), it is asked again after this long, not at once.
        private static readonly TimeSpan SpinGuard = TimeSpan.FromMilliseconds(250);

        // The least time that the engine remembers what OBS Studio removed (see RetentionFor).
        private static readonly TimeSpan MinimumRetention = TimeSpan.FromMinutes(5);

        private readonly ObsModel _model;
        private readonly ObsClient _client;
        private readonly ILogger _logger;
        private readonly TimeProvider _time;
        private readonly long _start;
        private readonly TimeSpan _readTimeout;
        private readonly ModelEngine _engine;
        private readonly ModelPublisher _publisher;
        private readonly ModelNotifier _notifier;
        private readonly ModelNoticeHold _held;
        private readonly Channel<HostItem> _inbox = Channel.CreateUnbounded<HostItem>(new UnboundedChannelOptions { SingleReader = true, AllowSynchronousContinuations = false });
        private readonly CancellationTokenSource _stopping = new();
        private readonly List<SettleWaiter> _settleWaiters = new();
        private readonly List<ConfirmWaiter> _confirmWaiters = new();
        private readonly List<TaskCompletionSource> _disconnectWaiters = new();
        private volatile WriteGate _gate = new(0, false, 0, 0);
        private int _settleWaiterCount;
        private int _confirmWaiterCount;
        private int _disconnectWaiterCount;
        private Task _loop = Task.CompletedTask;
        private ITimer? _timer;
        private TimeSpan? _armedFor;
        private ConnectAttempt? _attempt;
        private volatile bool _stopped;

        // What the loop knows about the connection. Only the loop reads and writes these.
        private int _session;
        private bool _versionKnown;
        private bool _versionInFlight;
        private TimeSpan? _versionRetryAt;
        private ReadTicket? _read;
        private int _readFailures;
        private TimeSpan _lastTime;

        // The events that begin and end a change of the scene collection of OBS Studio: how many the receive loop of the client has read (it counts them as it reads them), and how
        // many the loop of the model has handled (the loop counts them when it has applied them, and publishes the number in the gate). The model learns of a change of collection
        // when the loop handles the event, which can be some time after the client read it (the receive loop does not wait for the model), and the objects that the application holds
        // are the old collection's until then. A change that were written in that time would reach the collection that OBS Studio has loaded, with what the application learned
        // from the one that it left, so no change is written while the two numbers differ.
        private int _collectionEventsRead;
        private int _collectionEventsHandled;

        public ModelHost(ObsModel model, ObsClient client, ObsModelOptions options, ILogger logger, ModelNotifier notifier)
        {
            this._model = model;
            this._client = client;
            this._logger = logger;
            this._notifier = notifier;
            this._time = options.TimeProvider ?? TimeProvider.System;
            this._start = this._time.GetTimestamp();
            this._readTimeout = options.ReadTimeout;
            this._engine = new ModelEngine(new ModelEngineOptions { TombstoneRetention = RetentionFor(options.ReadTimeout), OutputStepTimeout = options.OutputStepTimeout });
            this._held = new ModelNoticeHold(options.MaxPendingNotifications);
            this._publisher = new ModelPublisher(model, options.IgnoreCase);
        }

        private enum HostItemKind
        {
            Ingress,
            VersionDone,
            VersionFailed,
            ReadDone,
            ReadFailed,
            Wake,
            Settle,
            Confirm,
            Refresh,
            Disconnect,
            ReconnectChanged,
            Prune,
            Recheck,
        }

        /// <summary>
        /// What a change that the application makes needs to know about the connection and the scene collection, from any thread: the loop publishes it after every item.
        /// </summary>
        /// <param name="Session">The connection that the model follows, or 0 when there is none.</param>
        /// <param name="Switching">Whether OBS Studio is changing its scene collection: it takes no change meanwhile.</param>
        /// <param name="Epoch">The number of the scene collection that the model is of; it goes up each time that OBS Studio has changed it.</param>
        /// <param name="CollectionEvents">How many events that begin or end a change of the scene collection the loop has handled (see <see cref="CollectionEventsRead"/>).</param>
        internal sealed record WriteGate(int Session, bool Switching, int Epoch, int CollectionEvents);

        /// <summary>Gets what a change that the application makes needs to know about the connection and the scene collection.</summary>
        public WriteGate Gate => this._gate;

        /// <summary>
        /// Gets how many events that begin or end a change of the scene collection the client has read. When it is not <see cref="WriteGate.CollectionEvents"/> of the gate,
        /// the loop of the model has not handled all of them yet, and what the model shows (and the objects that the application holds) is of the collection before them.
        /// </summary>
        public int CollectionEventsRead => Volatile.Read(ref this._collectionEventsRead);

        /// <summary>Gets what is published now.</summary>
        public ModelView View => this._publisher.View;

        /// <summary>
        /// Returns how long the engine remembers what OBS Studio removed before it lets go of it: the messages that it guards against (a read that was sent before the
        /// removal, the second copy of an event, an event that is late) arrive within the time that a read can take, so it is the larger of five minutes and four times
        /// the time that a read is given.
        /// </summary>
        /// <param name="readTimeout">The time that a read is given.</param>
        /// <returns>The retention.</returns>
        internal static TimeSpan RetentionFor(TimeSpan readTimeout)
        {
            var wanted = TimeSpan.FromTicks(readTimeout.Ticks * 4);
            return wanted > MinimumRetention ? wanted : MinimumRetention;
        }

        /// <summary>Gets a task that completes when the loop has ended.</summary>
        public Task Completion => this._loop;

        /// <summary>
        /// Gets or sets what is called with the time that the loop gives each item (see <see cref="TimeOf"/>), before it handles it. It is for tests.
        /// </summary>
        internal Action<TimeSpan>? BeforeItemHandled { get; set; }

        /// <summary>Gets the engine. It is for tests.</summary>
        internal ModelEngine Engine => this._engine;

        /// <summary>
        /// Gets or sets what is called with each item that comes from the client before the loop handles it. It is for tests: one can hold the loop there, to
        /// bring about an order in which things reach it.
        /// </summary>
        internal Action<IngressItem>? BeforeIngress { get; set; }

        /// <summary>
        /// Gets or sets what is called with each item that comes from the client after it was put in the inbox, on the thread of the client. It is for tests:
        /// one can tell that something is queued behind what the loop is held at.
        /// </summary>
        internal Action<IngressItem>? AfterIngressPosted { get; set; }

        /// <summary>
        /// Gets or sets what is called with a read before the loop gives its result to the engine. It is for tests: one can make it throw, as a fault in the
        /// model would.
        /// </summary>
        internal Action<ReadTicket>? BeforeReadApplied { get; set; }

        /// <summary>Gets the number of waits for the model to settle that the loop holds. It is for tests.</summary>
        internal int SettleWaiterCount => Volatile.Read(ref this._settleWaiterCount);

        /// <summary>Gets the number of waits for the model to show something that the loop holds. It is for tests.</summary>
        internal int ConfirmWaiterCount => Volatile.Read(ref this._confirmWaiterCount);

        /// <summary>Gets the number of waits for the end of the connection that the loop holds. It is for tests.</summary>
        internal int DisconnectWaiterCount => Volatile.Read(ref this._disconnectWaiterCount);

        /// <summary>Starts the loop, and the observation of the client.</summary>
        public void Start()
        {
            if (!this._client.TryAttachIngressTap(this.OnIngress))
            {
                throw new InvalidOperationException("The ObsClient is used by another ObsModel already: an ObsClient can have only one.");
            }

            // The tap is this host's from here on: when something fails before the loop runs, it and the observation of the client are let go of, or a client
            // that the application owns could not be given to another ObsModel.
            try
            {
                this._client.PropertyChanged += this.OnClientPropertyChanged;
                this._timer = this._time.CreateTimer(_ => this.Post(new HostItem { Kind = HostItemKind.Wake }), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                this._loop = Task.Run(this.RunAsync);
            }
            catch
            {
                this.Stop();
                throw;
            }
        }

        /// <summary>Stops the loop, and the observation of the client.</summary>
        public void Stop()
        {
            this._stopped = true;
            this._client.PropertyChanged -= this.OnClientPropertyChanged;
            this._client.SetIngressTap(null);
            this._inbox.Writer.TryComplete();
            this._stopping.Cancel();
            this._timer?.Dispose();
            this._notifier.Stop();
            var stopped = new ObjectDisposedException(nameof(ObsModel));
            Volatile.Read(ref this._attempt)?.TrySetException(stopped);
        }

        /// <summary>Makes ready for a connection: the attempt that is returned completes when the model has been read, or fails when it cannot be.</summary>
        public ConnectAttempt BeginConnect()
        {
            var attempt = new ConnectAttempt(this._client.LastIngress);
            Volatile.Write(ref this._attempt, attempt);
            return attempt;
        }

        /// <summary>Ends what <see cref="BeginConnect"/> began.</summary>
        public void EndConnect(ConnectAttempt attempt)
        {
            Interlocked.CompareExchange(ref this._attempt, null, attempt);
        }

        /// <summary>
        /// Returns a task that completes when everything that had arrived before the call has been applied and no read is pending. When the token ends first,
        /// the task is cancelled and the loop forgets the wait.
        /// </summary>
        public Task WaitUntilSettledAsync(CancellationToken cancellationToken)
        {
            return this.AddWaiter(HostItemKind.Settle, cancellationToken);
        }

        /// <summary>
        /// Returns a task that completes when <paramref name="predicate"/> is true: it is looked at when the wait is made (unless it is held back, see <paramref name="order"/>),
        /// and after every step of the loop that publishes what the model holds. The predicate runs on the loop, so it must only read the objects of the model, and be quick.
        /// When the token ends first, the task is cancelled and the loop forgets the wait; when the connection ends or the loop stops, the task fails.
        /// </summary>
        /// <param name="predicate">What is waited for.</param>
        /// <param name="cancellationToken">A token that ends the wait.</param>
        /// <param name="order">
        /// The parts of the model that what is waited for is about, or null. The waits that name the same part are looked at in the order in which they were made, one
        /// after the other: a wait is not looked at until the waits before it have what they waited for (or have ended). So a change to the value that the model still
        /// shows from before an earlier change is not taken for shown at once, and the wait that follows an earlier one is looked at in the same step of the loop in which
        /// the earlier one is done, so it does not miss the value that the model passes through.
        /// </param>
        /// <returns>A task that completes when the predicate is true.</returns>
        public Task ConfirmAsync(Func<bool> predicate, CancellationToken cancellationToken, IReadOnlyList<RefreshTarget>? order = null)
        {
            return this.AddWaiter(HostItemKind.Confirm, cancellationToken, predicate, order);
        }

        /// <summary>
        /// Asks the model to read the given parts again, and returns a task that completes when it has read those parts and applied the answers. What else the model has to read
        /// is not waited for: another part that keeps failing does not keep this wait from ending.
        /// </summary>
        /// <param name="targets">The parts.</param>
        /// <param name="cancellationToken">A token that ends the wait (the parts are read all the same).</param>
        /// <returns>A task that completes when the parts have been read again.</returns>
        public Task RefreshAsync(IReadOnlyList<RefreshTarget> targets, CancellationToken cancellationToken)
        {
            return this.AddWaiter(HostItemKind.Refresh, cancellationToken, targets: targets);
        }

        /// <summary>
        /// Asks the loop to look at the waits for the model to show something again (<see cref="ConfirmAsync"/>), as it does after every item that it handles. For whoever
        /// changed what a predicate reads after the loop had looked at it, so that the loop would not look at it again before the next item (which can be long in coming): the end
        /// of a read that a wait asked for, which the loop can have handled before the wait had written the read down.
        /// </summary>
        public void Recheck()
        {
            _ = this.Post(new HostItem { Kind = HostItemKind.Recheck });
        }

        /// <summary>
        /// Returns a task that completes when the connection that the client had is over, and the model knows. It is for the end of the connection that the
        /// application just asked for; when the model has not been told within the time, it is a <see cref="TimeoutException"/>, and the loop forgets the wait.
        /// </summary>
        public async Task WaitUntilDisconnectedAsync(TimeSpan timeout)
        {
            using var limit = new CancellationTokenSource(timeout);
            try
            {
                await this.AddWaiter(HostItemKind.Disconnect, limit.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (limit.IsCancellationRequested)
            {
                throw new TimeoutException($"The model was not told within {timeout} that the connection ended.");
            }
        }

        // Asks the loop to keep a wait, and to forget it when the token ends: a wait that ended without being forgotten would stay in the loop for as long as the
        // model does not settle, or the connection does not end.
        private Task AddWaiter(HostItemKind kind, CancellationToken cancellationToken, Func<bool>? predicate = null, IReadOnlyList<RefreshTarget>? targets = null)
        {
            var waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!this.Post(new HostItem { Kind = kind, Waiter = waiter, Predicate = predicate, Targets = targets }))
            {
                // The loop has ended: nothing will end a connection or settle the model any more.
                if (kind == HostItemKind.Disconnect)
                {
                    waiter.TrySetResult();
                }
                else
                {
                    waiter.TrySetException(new ObjectDisposedException(nameof(ObsModel)));
                }

                return waiter.Task;
            }

            if (cancellationToken.CanBeCanceled)
            {
                var registration = cancellationToken.Register(() =>
                {
                    waiter.TrySetCanceled(cancellationToken);
                    this.Post(new HostItem { Kind = HostItemKind.Prune });
                });
                _ = waiter.Task.ContinueWith(_ => registration.Dispose(), TaskScheduler.Default);
            }

            return waiter.Task;
        }

        // Called by the receive loop of the client: it only puts the item in the inbox. Events that the model does not need are not even queued.
        private void OnIngress(IngressItem item)
        {
            if (item.Kind == IngressKind.Event && !ModelEngine.Handles(item.Event!.EventType))
            {
                return;
            }

            if (item.Kind == IngressKind.Event && IsCollectionEvent(item.Event!.EventType))
            {
                // Counted before the item is queued, so that the loop never handles more of them than were read.
                Interlocked.Increment(ref this._collectionEventsRead);
            }

            this.Post(new HostItem { Kind = HostItemKind.Ingress, Ingress = item });
            this.AfterIngressPosted?.Invoke(item);
        }

        private static bool IsCollectionEvent(EventType eventType)
        {
            return eventType is EventType.CurrentSceneCollectionChanging or EventType.CurrentSceneCollectionChanged;
        }

        // The client decides by itself, a little after it has reported the end of a connection, that it will not reconnect (OBS Studio ended the session on
        // purpose), and the application can change the setting: what the state says about reconnecting follows it.
        private void OnClientPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ObsClient.AutoReconnect))
            {
                this.Post(new HostItem { Kind = HostItemKind.ReconnectChanged });
            }
        }

        private bool Post(in HostItem item)
        {
            return this._inbox.Writer.TryWrite(item with { Stamp = this._time.GetTimestamp() });
        }

        // The time since the host was made.
        private TimeSpan Now()
        {
            return this._time.GetElapsedTime(this._start);
        }

        // The time that the loop gives an item (and so the engine) is the time at which the item was put in the inbox, not the time at which the loop gets to it. When the
        // loop is late (it was held, or the computer slept with the connection open), the time that passes while an item waits must not count: the engine lets go of what
        // it remembers about removals after a time, and the events that it remembers them for can be queued behind. The items are put in the inbox by several threads,
        // so the time is not allowed to go back.
        private TimeSpan TimeOf(in HostItem item)
        {
            var time = this._time.GetElapsedTime(this._start, item.Stamp);
            if (time < this._lastTime)
            {
                time = this._lastTime;
            }

            this._lastTime = time;
            return time;
        }

        private async Task RunAsync()
        {
            var reader = this._inbox.Reader;
            try
            {
                while (await reader.WaitToReadAsync().ConfigureAwait(false))
                {
                    while (reader.TryRead(out var item))
                    {
                        try
                        {
                            this.Handle(item);
                        }
                        catch (Exception ex)
                        {
                            this.RecoverFrom(ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                this._logger.LogCritical(ex, "The loop of the model ended unexpectedly.");
            }
            finally
            {
                // Nothing will settle the model or end a connection any more, and nothing will raise what was held for the application.
                this.FailWaiters(new ObjectDisposedException(nameof(ObsModel)));
                this.ReleaseDisconnectWaiters();
                this._held.Clear();
                this.PublishWaiterCounts();
            }
        }

        private void Handle(in HostItem item)
        {
            var now = this.TimeOf(item);
            this.BeforeItemHandled?.Invoke(now);
            var before = this._engine.State;
            switch (item.Kind)
            {
                case HostItemKind.Ingress:
                    this.HandleIngress(item.Ingress, now);
                    break;
                case HostItemKind.VersionDone:
                    this.HandleVersion(item, now);
                    break;
                case HostItemKind.VersionFailed:
                    if (item.Session == this._session)
                    {
                        this._versionInFlight = false;
                        this._versionRetryAt = now + VersionRetryDelay;
                        this._logger.LogDebug(item.Error, "GetVersion failed; it is repeated.");
                    }

                    break;
                case HostItemKind.ReadDone:
                    if (this.TakeRead(item))
                    {
                        this._readFailures = 0;
                        this.BeforeReadApplied?.Invoke(item.Ticket!);
                        this._engine.OnReadCompleted(item.Ticket!, item.Results!, now);
                    }

                    break;
                case HostItemKind.ReadFailed:
                    if (this.TakeRead(item))
                    {
                        this._engine.OnReadFailed(item.Ticket!, now);
                        if (this._readFailures++ == 0)
                        {
                            this._logger.LogWarning(item.Error, "A read of the model of OBS Studio failed; it is repeated.");
                        }
                        else
                        {
                            this._logger.LogDebug(item.Error, "A read of the model of OBS Studio failed again.");
                        }
                    }

                    break;
                case HostItemKind.Wake:
                    this._armedFor = null;
                    break;
                case HostItemKind.ReconnectChanged:
                    this.ReconsiderReconnecting();
                    break;
                case HostItemKind.Settle:
                    if (this._engine.State == ModelEngineState.Disconnected)
                    {
                        item.Waiter!.TrySetException(new ObsNotConnectedException());
                    }
                    else
                    {
                        this._settleWaiters.Add(new SettleWaiter(item.Waiter!, null));
                    }

                    break;
                case HostItemKind.Confirm:
                    if (this._engine.State == ModelEngineState.Disconnected)
                    {
                        item.Waiter!.TrySetException(new ObsNotConnectedException());
                    }
                    else
                    {
                        this._confirmWaiters.Add(new ConfirmWaiter(item.Predicate!, item.Waiter!, item.Targets));
                    }

                    break;
                case HostItemKind.Refresh:
                    if (this._engine.State == ModelEngineState.Disconnected)
                    {
                        item.Waiter!.TrySetException(new ObsNotConnectedException());
                    }
                    else
                    {
                        foreach (var target in item.Targets!)
                        {
                            this._engine.RequestRefresh(target.Kind, target.Target, now);
                        }

                        // The wait is for these parts to be read, and they were marked in this same step: it can not be done before they are read.
                        this._settleWaiters.Add(new SettleWaiter(item.Waiter!, item.Targets));
                    }

                    break;
                case HostItemKind.Disconnect:
                    if (this._session == 0)
                    {
                        // No connection is left to wait for. What the client was going to restore ends here; a call of ConnectAsync that is under way owns the
                        // state (Connecting, or what the end of its connection left), and says Disconnected itself when it has unwound: Disconnected here would let
                        // the application connect again while that call is still on its way.
                        this._model.TrySetState(ObsModelState.Reconnecting, ObsModelState.Disconnected);
                        item.Waiter!.TrySetResult();
                    }
                    else
                    {
                        this._disconnectWaiters.Add(item.Waiter!);
                    }

                    break;
                case HostItemKind.Prune:
                    this._settleWaiters.RemoveAll(static waiter => waiter.Waiter.Task.IsCompleted);
                    this._confirmWaiters.RemoveAll(static waiter => waiter.Done.Task.IsCompleted);
                    this._disconnectWaiters.RemoveAll(static waiter => waiter.Task.IsCompleted);
                    break;
                case HostItemKind.Recheck:
                    // Nothing to do here: what comes after every item looks at the waits again (see Recheck).
                    break;
            }

            this.PumpReads(now);
            this.PublishStep(before);
            this.PublishGate();
            this.ReleaseWaiters();
            this.ReleaseConfirmations();
            this.ArmTimer(this.Now());
            this.PublishWaiterCounts();
        }

        // An event that begins or ends a change of the scene collection is counted when the loop is done with it, whether the model applied it or failed to (it reads everything
        // again then, see RecoverFrom): the client counted it when it read it, and a count that fell behind for good would keep every change from being made.
        private void HandleIngress(IngressItem ingress, TimeSpan now)
        {
            try
            {
                this.ApplyIngress(ingress, now);
            }
            finally
            {
                if (ingress.Kind == IngressKind.Event && IsCollectionEvent(ingress.Event!.EventType))
                {
                    // Whatever connection it came on: the gate is published after this step.
                    this._collectionEventsHandled++;
                }
            }
        }

        private void ApplyIngress(IngressItem ingress, TimeSpan now)
        {
            this.BeforeIngress?.Invoke(ingress);
            switch (ingress.Kind)
            {
                case IngressKind.SessionStarted:
                    if (!this.Wants(ingress))
                    {
                        // A connection that nobody asked for: the call of ConnectAsync that made it was ended (a token, DisconnectAsync, a disposal), and has unwound,
                        // and its start reached the loop late; or the client made it by itself (a reconnect that was queued when DisconnectAsync ended the one
                        // before). It is not followed: that would publish Synchronizing for a connection that is on its way out, and refuse the application that
                        // connects again in that moment. And it is not left connected and untracked either: the connection of that session is ended (a connection
                        // that is ending already stays as it is, and so does one that was made in its place).
                        this.EndSessionOffLoop(ingress.SessionId);
                        break;
                    }

                    this._session = ingress.SessionId;
                    this._versionKnown = false;
                    this._versionRetryAt = null;
                    this._read = null;
                    this._readFailures = 0;

                    // The attempt that is under way is for this connection when the connection started after the attempt began: a start that was already
                    // on its way when it began (the loop was late with it) belongs to an earlier attempt, and so does the end that follows it.
                    if (Volatile.Read(ref this._attempt) is { Session: 0 } attempt && ingress.Sequence > attempt.After)
                    {
                        attempt.Session = ingress.SessionId;
                    }

                    // The OBS Studio of this connection may not be the one of the connection before (a reconnect): what that one said about the names of removed scenes is not kept, and
                    // the connection says its own with its version (HandleVersion).
                    this._model.SetObsVersion(null);
                    this._engine.OnSessionStarted(ingress.SessionId, ingress.Sequence, now);
                    this._model.SetState(ObsModelState.Synchronizing);
                    this.StartVersionCheck(ingress.SessionId);
                    break;
                case IngressKind.SessionEnded:
                    if (ingress.SessionId != this._session)
                    {
                        break;
                    }

                    this._session = 0;
                    this._versionKnown = false;
                    this._versionInFlight = false;
                    this._versionRetryAt = null;
                    this._read = null;
                    this._engine.OnSessionEnded(ingress.SessionId, ingress.Sequence, now);
                    this.FailWaiters(new ObsNotConnectedException("The connection to OBS Studio ended."));

                    // The end of a connection can reach the loop after a retry has begun (it comes through the same inbox as everything else, and the
                    // client says that it is disconnected before that): it decides an attempt that is for this connection, and leaves another one alone, and
                    // so it does the state, which is the state of the attempt that is under way.
                    var current = Volatile.Read(ref this._attempt);
                    if (current is null || current.Session == ingress.SessionId)
                    {
                        // A call of ConnectAsync that has not succeeded owns the state until it has unwound: it ends the connection, and then says
                        // Disconnected. A state that is published before (Disconnected, or Reconnecting) would let the application connect again while
                        // the older call is still on its way to end the connection that it made.
                        var owned = current is not null && !current.Task.IsCompletedSuccessfully;
                        current?.TrySetException(new ObsNotConnectedException("The connection to OBS Studio ended before the model was complete."));
                        if (!owned)
                        {
                            this._model.SetState(this._client.AutoReconnect ? ObsModelState.Reconnecting : ObsModelState.Disconnected);
                        }
                    }

                    this.ReleaseDisconnectWaiters();
                    break;
                case IngressKind.Event:
                    if (ingress.SessionId == this._session)
                    {
                        this._engine.OnEvent(ingress.Event!, ingress.Sequence, now);
                    }

                    break;
            }
        }

        // Whether the model follows the connection whose start this is: the connection that the call of ConnectAsync that is under way made (it started after the
        // attempt began, and no connection is bound to the attempt yet), or the one that the client restored after it lost one (the state is Reconnecting, which only
        // the loop changes while it lasts). The client connects by itself in no other case.
        private bool Wants(IngressItem ingress)
        {
            return (Volatile.Read(ref this._attempt) is { Session: 0 } attempt && ingress.Sequence > attempt.After)
                || this._model.State == ObsModelState.Reconnecting;
        }

        private void HandleVersion(in HostItem item, TimeSpan now)
        {
            if (item.Session != this._session)
            {
                return;
            }

            this._versionInFlight = false;
            var reported = item.Version!.ObsWebSocketVersion;
            if (TryParseVersion(reported, out var version) && version >= RequiredWebSocketVersion)
            {
                this._versionKnown = true;

                // What a removed scene does to its name depends on the version of OBS Studio (libobs), not on that of obs-websocket. A version that can not be read gives the newest behaviour (null).
                var obsVersion = TryParseVersion(item.Version.ObsVersion, out var parsedObsVersion) ? parsedObsVersion : null;
                this._model.SetObsVersion(obsVersion);
                this._model.SetVersionFacts(item.Version, obsVersion, version);

                return;
            }

            // The model needs UUIDs. It does not read from an OBS Studio that does not have them; the client itself works with it.
            var unsupported = new ObsVersionNotSupportedException(reported, RequiredWebSocketVersion.ToString(3));
            this._logger.LogError("{Message}", unsupported.Message);
            this.FailAttempt(item.Session, unsupported);
            this.EndSessionOffLoop(item.Session);
        }

        // Ends the connection of a session, which the model rejected. Nothing that belongs to the application runs on the loop, and ending a connection raises
        // the PropertyChanged event of the client (AutoReconnect) on the thread that does it: a handler that waits would hold up the model, and one that disposes of
        // the model would wait for the loop that runs it. It is the connection of that session that is ended, whenever this runs: what was made in its place is not.
        private void EndSessionOffLoop(int session)
        {
            _ = Task.Run(() =>
            {
                try
                {
                    this._client.EndSession(session);
                }
                catch (Exception ex)
                {
                    this._logger.LogDebug(ex, "Could not end the connection that the model rejected.");
                }
            });
        }

        // An attempt is decided by the connection that it is for, and by no other.
        private void FailAttempt(int session, Exception exception)
        {
            if (Volatile.Read(ref this._attempt) is { } attempt && attempt.Session == session)
            {
                attempt.TrySetException(exception);
            }
        }

        // The connection ended while the client was going to restore it, and it is not going to any more: nothing starts a connection but the application.
        private void ReconsiderReconnecting()
        {
            if (this._session == 0 && !this._client.AutoReconnect)
            {
                this._model.TrySetState(ObsModelState.Reconnecting, ObsModelState.Disconnected);
            }
        }

        private void CompleteAttempt(int session)
        {
            if (Volatile.Read(ref this._attempt) is { } attempt && attempt.Session == session)
            {
                attempt.TrySetResult();
            }
        }

        private bool TakeRead(in HostItem item)
        {
            if (item.Session != this._session || this._read is null || this._read.Id != item.Ticket!.Id)
            {
                // The read of a connection that ended: its parts are read again on the next one.
                return false;
            }

            this._read = null;
            return true;
        }

        private void StartVersionCheck(int session)
        {
            this._versionInFlight = true;
            _ = this.CheckVersionAsync(session);
        }

        private async Task CheckVersionAsync(int session)
        {
            try
            {
                var version = await this._client.GetVersion(this._stopping.Token).ConfigureAwait(false);
                this.Post(new HostItem { Kind = HostItemKind.VersionDone, Session = session, Version = version });
            }
            catch (Exception ex)
            {
                this.Post(new HostItem { Kind = HostItemKind.VersionFailed, Session = session, Error = ex });
            }
        }

        private void PumpReads(TimeSpan now)
        {
            if (this._read is not null && this._engine.IsOutdated(this._read))
            {
                // OBS Studio changed its scene collection after the read was taken: its answer will not be applied, and it must not hold up the reads of the new collection.
                // When it comes, or fails, it is not the read that the loop waits for (TakeRead).
                this._read = null;
            }

            if (this._session == 0 || this._read is not null)
            {
                return;
            }

            if (!this._versionKnown)
            {
                if (!this._versionInFlight && this._versionRetryAt is { } at && now >= at)
                {
                    this._versionRetryAt = null;
                    this.StartVersionCheck(this._session);
                }

                return;
            }

            if (this._engine.TryTakeRead(this._client.LastIngress, now, out var ticket))
            {
                this._read = ticket;
                _ = this.SendReadAsync(ticket, this._session);
            }
        }

        private async Task SendReadAsync(ReadTicket ticket, int session)
        {
            try
            {
                var batch = new RequestBatchMessage(haltOnFailure: false, RequestBatchExecutionType.SerialRealtime);
                foreach (var request in ticket.Requests)
                {
                    batch.AddRaw(request.Type, request.Data);
                }

                var response = await this._client.SendRawBatchAsync(batch, (int)this._readTimeout.TotalMilliseconds, this._stopping.Token).ConfigureAwait(false);
                this.Post(new HostItem { Kind = HostItemKind.ReadDone, Session = session, Ticket = ticket, Results = response.Results });
            }
            catch (Exception ex)
            {
                this.Post(new HostItem { Kind = HostItemKind.ReadFailed, Session = session, Ticket = ticket, Error = ex });
            }
        }

        // Publishes what the engine holds. The application is told of the changes one by one only when the model was complete before this
        // step and is after it. While it is being read the application is not told one by one: the objects are brought in line at once, but the
        // notifications of the properties are held, and raised once for each property when the model is complete again (see ModelNoticeHold),
        // with Resynchronized after them.
        private void PublishStep(ModelEngineState before)
        {
            var changes = this._engine.TakeChanges();
            var view = this._publisher.Publish(this._engine, changes);
            this._model.SetView(view);
            var after = this._engine.State;

            // What happened is told in the order of arrival and whatever the state of the model is: a file that was saved while the model was read was saved all the same. It follows the notices of the step.
            var events = this._publisher.Events;
            if (before == ModelEngineState.Synchronized && after == ModelEngineState.Synchronized)
            {
                foreach (var notice in this._publisher.Notices)
                {
                    this._model.QueueNotice(notice);
                }

                foreach (var happened in events)
                {
                    this._model.QueueNotice(happened);
                }

                return;
            }

            this._held.Hold(this._publisher.Notices);
            foreach (var happened in events)
            {
                this._model.QueueNotice(happened);
            }
            if (after == ModelEngineState.Synchronizing)
            {
                // OBS Studio is changing its scene collection, or has changed it and the model reads the new one: the model is not complete, and the application is told so.
                // The state of a connection that is being made or restored is not changed: whoever owns that decides it.
                this._model.TrySetState(ObsModelState.Synchronized, ObsModelState.Synchronizing);
            }

            if (after == ModelEngineState.Synchronized)
            {
                // The first time, the objects are new to the application, which cannot hold any (nor be bound to one): what it can be bound to is the
                // model itself, whose collections were empty before.
                var first = !this._model.EverSynchronized;
                this._model.SetState(ObsModelState.Synchronized);
                if (this._held.Release(this._model.QueueNotice, onlyModel: first))
                {
                    // More changed while the model was read than could be held (see ModelNoticeHold): everything may have changed, and the application is told so.
                    this._model.QueueEverything(onlyModel: first);
                }
                else
                {
                    this._model.QueueResynchronized();
                }

                this.CompleteAttempt(this._session);
            }
        }

        // The waits for the model to settle are done when nothing is left to read, anywhere; the waits for the parts that a refresh asked for, when those parts have been read, whatever
        // else the model has to read (another part that keeps failing would keep a call from ending that was about this one).
        private void ReleaseWaiters()
        {
            if (this._settleWaiters.Count == 0)
            {
                return;
            }

            if (this._engine.State == ModelEngineState.Disconnected)
            {
                this.FailWaiters(new ObsNotConnectedException());
                return;
            }

            var settled = this._versionKnown && this._read is null && this._engine.IsSettled;
            this._settleWaiters.RemoveAll(waiter =>
            {
                var done = waiter.Targets is null ? settled : this._versionKnown && this.AreRefreshed(waiter.Targets);
                if (done)
                {
                    waiter.Waiter.TrySetResult();
                }

                return done;
            });
        }

        // Whether every one of the parts has been read since it was asked for. It reads the engine, so it is for the loop only (the waits for the model to show something are looked at there).
        internal bool AreRefreshed(IReadOnlyList<RefreshTarget> targets)
        {
            foreach (var target in targets)
            {
                if (!this._engine.IsRefreshed(target.Kind, target.Target))
                {
                    return false;
                }
            }

            return true;
        }

        private void FailWaiters(Exception exception)
        {
            foreach (var waiter in this._settleWaiters)
            {
                waiter.Waiter.TrySetException(exception);
            }

            this._settleWaiters.Clear();
            foreach (var waiter in this._confirmWaiters)
            {
                waiter.Done.TrySetException(exception);
            }

            this._confirmWaiters.Clear();
        }

        // Whoever waits for the model to show something has what it waited for, or is told when the predicate fails. The predicates read the objects that were just
        // published, so this comes after PublishStep.
        //
        // The waits that are about the same part of the model are looked at in the order in which they were made, one after the other, and in this same pass when the one before
        // has what it waited for. A change can be made right after the change of the wait before it, and the model passes through both values: a wait that was looked at too early
        // would find the value that the model still shows from before the change before it (and the call would return before the model showed its change), and one that was looked
        // at too late would find that the model had passed the value that it waits for.
        private void ReleaseConfirmations()
        {
            HashSet<RefreshTarget>? held = null;
            var i = 0;
            while (i < this._confirmWaiters.Count)
            {
                var waiter = this._confirmWaiters[i];
                if (waiter.Done.Task.IsCompleted)
                {
                    this._confirmWaiters.RemoveAt(i);
                    continue;
                }

                if (held is not null && IsHeldBack(waiter.Order, held))
                {
                    // A wait before this one, about the same part of the model, has not got what it waited for. This one has not either, and so it holds back the waits after it
                    // for all of its parts, also for the ones that the wait before it does not name.
                    Hold(ref held, waiter.Order);
                    i++;
                    continue;
                }

                try
                {
                    if (waiter.Predicate())
                    {
                        waiter.Done.TrySetResult();
                        this._confirmWaiters.RemoveAt(i);
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    waiter.Done.TrySetException(ex);
                    this._confirmWaiters.RemoveAt(i);
                    continue;
                }

                Hold(ref held, waiter.Order);
                i++;
            }
        }

        private static void Hold(ref HashSet<RefreshTarget>? held, IReadOnlyList<RefreshTarget>? order)
        {
            if (order is not { Count: > 0 })
            {
                return;
            }

            held ??= [];
            foreach (var part in order)
            {
                held.Add(part);
            }
        }

        private static bool IsHeldBack(IReadOnlyList<RefreshTarget>? order, HashSet<RefreshTarget> held)
        {
            if (order is null)
            {
                return false;
            }

            foreach (var part in order)
            {
                if (held.Contains(part))
                {
                    return true;
                }
            }

            return false;
        }

        // What a change that the application makes needs to know: published after every item, and only made new when it differs.
        private void PublishGate()
        {
            var gate = this._gate;
            var switching = this._engine.IsSwitching;
            var epoch = this._engine.Epoch;
            if (gate.Session != this._session || gate.Switching != switching || gate.Epoch != epoch || gate.CollectionEvents != this._collectionEventsHandled)
            {
                this._gate = new WriteGate(this._session, switching, epoch, this._collectionEventsHandled);
            }
        }

        // The connection is over, or the loop has ended: whoever waits for the end of the connection has what it waited for.
        private void ReleaseDisconnectWaiters()
        {
            foreach (var waiter in this._disconnectWaiters)
            {
                waiter.TrySetResult();
            }

            this._disconnectWaiters.Clear();
        }

        private void PublishWaiterCounts()
        {
            Volatile.Write(ref this._settleWaiterCount, this._settleWaiters.Count);
            Volatile.Write(ref this._confirmWaiterCount, this._confirmWaiters.Count);
            Volatile.Write(ref this._disconnectWaiterCount, this._disconnectWaiters.Count);
        }

        // Wakes the loop when the engine wants to read something later (a read that waits for its turn, or that is repeated after a wait).
        private void ArmTimer(TimeSpan now)
        {
            if (this._stopped)
            {
                return;
            }

            TimeSpan? wake = null;
            if (this._session != 0)
            {
                if (!this._versionKnown)
                {
                    wake = this._versionInFlight ? null : this._versionRetryAt;
                }
                else if (this._read is null)
                {
                    wake = this._engine.NextWake;
                }
            }

            if (wake is null)
            {
                if (this._armedFor is not null)
                {
                    this.ChangeTimer(Timeout.InfiniteTimeSpan);
                    this._armedFor = null;
                }

                return;
            }

            var due = wake.Value - now;
            if (due < SpinGuard && wake.Value <= now)
            {
                due = SpinGuard;
            }

            if (due < TimeSpan.Zero)
            {
                due = TimeSpan.Zero;
            }

            if (this._armedFor != wake)
            {
                this.ChangeTimer(due);
                this._armedFor = wake;
            }
        }

        // The timer is disposed by Stop, on another thread, while the loop may still be finishing an item.
        private void ChangeTimer(TimeSpan due)
        {
            try
            {
                this._timer?.Change(due, Timeout.InfiniteTimeSpan);
            }
            catch (ObjectDisposedException)
            {
            }
        }

        // An exception in the model is a bug. The model cannot know what it missed, so it reads everything again; the application sees the old
        // values until that is done.
        private void RecoverFrom(Exception exception)
        {
            this._logger.LogError(exception, "The model failed while it processed an item. It reads everything again.");
            if (this._session == 0)
            {
                return;
            }

            try
            {
                this._read = null;
                this._engine.OnSessionStarted(this._session, this._client.LastIngress, this._lastTime);
                this._model.SetState(ObsModelState.Synchronizing);

                // The item that failed did not get to the part of the loop that sends the reads that the engine wants and arms the timer: nothing else is
                // on its way that would wake the loop, so it is woken.
                this.Post(new HostItem { Kind = HostItemKind.Wake });
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "The model could not recover.");
            }
        }

        // obs-websocket versions look like 5.7.6; a suffix such as -beta1 is not part of the number.
        private static bool TryParseVersion(string? text, out Version version)
        {
            version = new Version(0, 0);
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var end = text.IndexOfAny(['-', '+', ' ']);
            var number = end < 0 ? text : text[..end];
            if (!Version.TryParse(number, out var parsed))
            {
                return false;
            }

            version = parsed;
            return true;
        }

        // A wait for the model to settle (Targets is null: nothing is left to read, anywhere) or for the parts that a refresh asked for to be read (Targets: nothing else matters).
        private sealed record SettleWaiter(TaskCompletionSource Waiter, IReadOnlyList<RefreshTarget>? Targets);

        // A wait for the model to show something: the predicate is looked at after every step that publishes (ReleaseConfirmations). The waits that are about the same part of the
        // model (Order) are looked at one after the other.
        private sealed record ConfirmWaiter(Func<bool> Predicate, TaskCompletionSource Done, IReadOnlyList<RefreshTarget>? Order);

        private readonly struct HostItem
        {
            public HostItemKind Kind { get; init; }

            public IngressItem Ingress { get; init; }

            public int Session { get; init; }

            public ReadTicket? Ticket { get; init; }

            public RequestResponseMessage[]? Results { get; init; }

            public VersionResponse? Version { get; init; }

            public Exception? Error { get; init; }

            public TaskCompletionSource? Waiter { get; init; }

            public Func<bool>? Predicate { get; init; }

            public IReadOnlyList<RefreshTarget>? Targets { get; init; }

            // The time at which the item was put in the inbox (a timestamp of the time provider), which is the time that the loop gives it.
            public long Stamp { get; init; }
        }

        /// <summary>
        /// One call of <see cref="ObsModel.ConnectAsync"/>. It is for the first connection that starts after it began, and only the end or the failure of that
        /// connection, or of the model that it carries, decides it: a connection that started before it began (its start can still be in the inbox, when the
        /// loop is late) and the end of such a connection are not for it.
        /// </summary>
        public sealed class ConnectAttempt
        {
            private readonly TaskCompletionSource _source = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public ConnectAttempt(long after)
            {
                this.After = after;
            }

            /// <summary>Gets the task that completes when the model has been read, or fails when it cannot be.</summary>
            public Task Task => this._source.Task;

            /// <summary>Gets the number of the last message that the client had read when the attempt began: a connection that started with a lower number is not the one.</summary>
            public long After { get; }

            /// <summary>Gets or sets the connection that the attempt is for, or 0 until that connection starts. Only the loop reads and writes it.</summary>
            public int Session { get; set; }

            public bool TrySetResult()
            {
                return this._source.TrySetResult();
            }

            public bool TrySetException(Exception exception)
            {
                return this._source.TrySetException(exception);
            }
        }
    }
}
