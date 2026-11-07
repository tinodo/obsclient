namespace OBSStudioClient.Studio
{
    using System.ComponentModel;
    using System.Threading.Channels;
    using Microsoft.Extensions.Logging;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Messages;
    using OBSStudioClient.Responses;
    using OBSStudioClient.Studio.Engine;

    /// <summary>
    /// Runs the model of an <see cref="ObsStudio"/>: the one loop that takes what arrives (the events and the start and end of connections, each
    /// with the number that the receive loop of the <see cref="ObsClient"/> gave it on arrival, and the results of the reads that it asked for), gives
    /// it to the <see cref="StudioEngine"/> one item at a time, sends the reads that the engine wants, and publishes the outcome.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nothing that belongs to the application runs on this loop: not a handler, not a callback. The notifications are queued, and raised by the
    /// <see cref="StudioNotifier"/>. The receive loop of the client only puts events in the inbox, which never blocks, so a slow application cannot hold up
    /// the reading of OBS Studio, or the model.
    /// </para>
    /// <para>
    /// Reads are sent one at a time. What arrives while a read is under way is processed meanwhile, in the order of arrival; the engine knows
    /// from the numbers which events are newer than a read.
    /// </para>
    /// </remarks>
    internal sealed class StudioHost
    {
        /// <summary>The events that the model needs. They are always subscribed to, with the ones that the application asks for.</summary>
        public const EventSubscriptions RequiredEvents =
            EventSubscriptions.Config | EventSubscriptions.Scenes | EventSubscriptions.Inputs | EventSubscriptions.SceneItems | EventSubscriptions.Ui;

        /// <summary>The oldest obs-websocket that the model works with: the first one that identifies scenes and inputs by UUID (OBS Studio 30.1).</summary>
        public static readonly Version RequiredWebSocketVersion = new(5, 4, 0);

        private static readonly TimeSpan VersionRetryDelay = TimeSpan.FromSeconds(1);

        // When the engine wants a read that it does not hand out (it should not happen), it is asked again after this long, not at once.
        private static readonly TimeSpan SpinGuard = TimeSpan.FromMilliseconds(250);

        private readonly ObsStudio _studio;
        private readonly ObsClient _client;
        private readonly ILogger _logger;
        private readonly TimeProvider _time;
        private readonly long _start;
        private readonly TimeSpan _readTimeout;
        private readonly StudioEngine _engine = new();
        private readonly StudioPublisher _publisher;
        private readonly StudioNotifier _notifier;
        private readonly StudioNoticeHold _held;
        private readonly Channel<HostItem> _inbox = Channel.CreateUnbounded<HostItem>(new UnboundedChannelOptions { SingleReader = true, AllowSynchronousContinuations = false });
        private readonly CancellationTokenSource _stopping = new();
        private readonly List<TaskCompletionSource> _settleWaiters = new();
        private readonly List<TaskCompletionSource> _disconnectWaiters = new();
        private int _settleWaiterCount;
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

        public StudioHost(ObsStudio studio, ObsClient client, ObsStudioOptions options, ILogger logger, StudioNotifier notifier)
        {
            this._studio = studio;
            this._client = client;
            this._logger = logger;
            this._notifier = notifier;
            this._time = options.TimeProvider ?? TimeProvider.System;
            this._start = this._time.GetTimestamp();
            this._readTimeout = options.ReadTimeout;
            this._held = new StudioNoticeHold(options.MaxPendingNotifications);
            this._publisher = new StudioPublisher(studio, options.IgnoreCase);
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
            Disconnect,
            ReconnectChanged,
            Prune,
        }

        /// <summary>Gets what is published now.</summary>
        public StudioView View => this._publisher.View;

        /// <summary>Gets a task that completes when the loop has ended.</summary>
        public Task Completion => this._loop;

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

        /// <summary>Gets the number of waits for the end of the connection that the loop holds. It is for tests.</summary>
        internal int DisconnectWaiterCount => Volatile.Read(ref this._disconnectWaiterCount);

        /// <summary>Starts the loop, and the observation of the client.</summary>
        public void Start()
        {
            if (!this._client.TryAttachIngressTap(this.OnIngress))
            {
                throw new InvalidOperationException("The ObsClient is used by another ObsStudio already: an ObsClient can have only one.");
            }

            // The tap is this host's from here on: when something fails before the loop runs, it and the observation of the client are let go of, or a client
            // that the application owns could not be given to another ObsStudio.
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
            var stopped = new ObjectDisposedException(nameof(ObsStudio));
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
        private Task AddWaiter(HostItemKind kind, CancellationToken cancellationToken)
        {
            var waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!this.Post(new HostItem { Kind = kind, Waiter = waiter }))
            {
                // The loop has ended: nothing will end a connection or settle the model any more.
                if (kind == HostItemKind.Disconnect)
                {
                    waiter.TrySetResult();
                }
                else
                {
                    waiter.TrySetException(new ObjectDisposedException(nameof(ObsStudio)));
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
            if (item.Kind == IngressKind.Event && !StudioEngine.Handles(item.Event!.EventType))
            {
                return;
            }

            this.Post(new HostItem { Kind = HostItemKind.Ingress, Ingress = item });
            this.AfterIngressPosted?.Invoke(item);
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
            return this._inbox.Writer.TryWrite(item);
        }

        private TimeSpan Now()
        {
            return this._time.GetElapsedTime(this._start);
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
                this._logger.LogCritical(ex, "The loop of the studio model ended unexpectedly.");
            }
            finally
            {
                // Nothing will settle the model or end a connection any more, and nothing will raise what was held for the application.
                this.FailWaiters(new ObjectDisposedException(nameof(ObsStudio)));
                this.ReleaseDisconnectWaiters();
                this._held.Clear();
                this.PublishWaiterCounts();
            }
        }

        private void Handle(in HostItem item)
        {
            var now = this.Now();
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
                    if (this._engine.State == StudioEngineState.Disconnected)
                    {
                        item.Waiter!.TrySetException(new ObsNotConnectedException());
                    }
                    else
                    {
                        this._settleWaiters.Add(item.Waiter!);
                    }

                    break;
                case HostItemKind.Disconnect:
                    if (this._session == 0)
                    {
                        // No connection is left to wait for. What the client was going to restore ends here; a call of ConnectAsync that is under way owns the
                        // state (Connecting, or what the end of its connection left), and says Disconnected itself when it has unwound: Disconnected here would let
                        // the application connect again while that call is still on its way.
                        this._studio.TrySetState(ObsStudioState.Reconnecting, ObsStudioState.Disconnected);
                        item.Waiter!.TrySetResult();
                    }
                    else
                    {
                        this._disconnectWaiters.Add(item.Waiter!);
                    }

                    break;
                case HostItemKind.Prune:
                    this._settleWaiters.RemoveAll(static waiter => waiter.Task.IsCompleted);
                    this._disconnectWaiters.RemoveAll(static waiter => waiter.Task.IsCompleted);
                    break;
            }

            this.PumpReads(now);
            this.PublishStep(before);
            this.ReleaseWaiters();
            this.ArmTimer(now);
            this.PublishWaiterCounts();
        }

        private void HandleIngress(IngressItem ingress, TimeSpan now)
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

                    this._engine.OnSessionStarted(ingress.SessionId, ingress.Sequence, now);
                    this._studio.SetState(ObsStudioState.Synchronizing);
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
                            this._studio.SetState(this._client.AutoReconnect ? ObsStudioState.Reconnecting : ObsStudioState.Disconnected);
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
                || this._studio.State == ObsStudioState.Reconnecting;
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
        // the studio would wait for the loop that runs it. It is the connection of that session that is ended, whenever this runs: what was made in its place is not.
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
                this._studio.TrySetState(ObsStudioState.Reconnecting, ObsStudioState.Disconnected);
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
        // notifications of the properties are held, and raised once for each property when the model is complete again (see StudioNoticeHold),
        // with Resynchronized after them.
        private void PublishStep(StudioEngineState before)
        {
            var changes = this._engine.TakeChanges();
            var view = this._publisher.Publish(this._engine, changes);
            this._studio.SetView(view);
            var after = this._engine.State;
            if (before == StudioEngineState.Synchronized && after == StudioEngineState.Synchronized)
            {
                foreach (var notice in this._publisher.Notices)
                {
                    this._studio.QueueNotice(notice);
                }

                return;
            }

            this._held.Hold(this._publisher.Notices);
            if (after == StudioEngineState.Synchronized)
            {
                // The first time, the objects are new to the application, which cannot hold any (nor be bound to one): what it can be bound to is the
                // studio itself, whose collections were empty before.
                var first = !this._studio.EverSynchronized;
                this._studio.SetState(ObsStudioState.Synchronized);
                if (this._held.Release(this._studio.QueueNotice, onlyStudio: first))
                {
                    // More changed while the model was read than could be held (see StudioNoticeHold): everything may have changed, and the application is told so.
                    this._studio.QueueEverything(onlyStudio: first);
                }
                else
                {
                    this._studio.QueueResynchronized();
                }

                this.CompleteAttempt(this._session);
            }
        }

        private void ReleaseWaiters()
        {
            if (this._settleWaiters.Count == 0)
            {
                return;
            }

            if (this._engine.State == StudioEngineState.Disconnected)
            {
                this.FailWaiters(new ObsNotConnectedException());
                return;
            }

            if (this._versionKnown && this._read is null && this._engine.IsSettled)
            {
                foreach (var waiter in this._settleWaiters)
                {
                    waiter.TrySetResult();
                }

                this._settleWaiters.Clear();
            }
        }

        private void FailWaiters(Exception exception)
        {
            foreach (var waiter in this._settleWaiters)
            {
                waiter.TrySetException(exception);
            }

            this._settleWaiters.Clear();
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
            this._logger.LogError(exception, "The studio model failed while it processed an item. It reads everything again.");
            if (this._session == 0)
            {
                return;
            }

            try
            {
                this._read = null;
                this._engine.OnSessionStarted(this._session, this._client.LastIngress, this.Now());
                this._studio.SetState(ObsStudioState.Synchronizing);

                // The item that failed did not get to the part of the loop that sends the reads that the engine wants and arms the timer: nothing else is
                // on its way that would wake the loop, so it is woken.
                this.Post(new HostItem { Kind = HostItemKind.Wake });
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "The studio model could not recover.");
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
        }

        /// <summary>
        /// One call of <see cref="ObsStudio.ConnectAsync"/>. It is for the first connection that starts after it began, and only the end or the failure of that
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
