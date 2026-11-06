namespace OBSStudioClient.Studio
{
    using System.Collections.Concurrent;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Raises the notifications of an <see cref="ObsStudio"/> one at a time, in the order in which they were queued, outside the thread that runs
    /// the model: what a handler does, or how long it takes, cannot disturb the model. A handler that waits holds up only the notifications behind it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Like the notifications of the <see cref="ObsClient"/>: there is no task that waits for work; a task is started when something is queued
    /// and nothing is being delivered, and it ends when the queue is empty. With a <see cref="SynchronizationContext"/> the notifications are posted to it
    /// in order, and what it does with them (a user interface thread runs them one by one) is its business.
    /// </para>
    /// <para>
    /// The notifications that wait are bounded, so that handlers that are slower than OBS Studio (or that never return) cannot make the memory grow for ever. A
    /// notification that is waiting for its handlers counts until it has been raised, also when it was posted to a context that has not run it yet. When
    /// more than the capacity wait, the new notifications are dropped, and one more is queued after the ones that wait: the recovery, which says that
    /// everything may have changed. It is raised when the handlers get to it, and notifications are accepted again from there.
    /// </para>
    /// </remarks>
    internal sealed class StudioNotifier
    {
        private const int BacklogWarning = 1000;

        private readonly ConcurrentQueue<Action> _queue = new();
        private readonly object _gate = new();
        private readonly SynchronizationContext? _context;
        private readonly ILogger _logger;
        private readonly int _capacity;
        private readonly Action _recover;
        private readonly Action _recovery;
        private int _waiting;
        private int _delivering;
        private bool _overflowed;
        private bool _postFailed;
        private volatile bool _stopped;

        /// <summary>Initializes a new instance of the <see cref="StudioNotifier"/> class.</summary>
        /// <param name="context">The context to raise the notifications on, or null for a thread of the thread pool.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="capacity">The number of notifications that may wait.</param>
        /// <param name="recover">What is raised when notifications were dropped: it tells the application that everything may have changed.</param>
        public StudioNotifier(SynchronizationContext? context, ILogger logger, int capacity, Action recover)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
            this._context = context;
            this._logger = logger;
            this._capacity = capacity;
            this._recover = recover;
            this._recovery = this.Recover;
        }

        /// <summary>Gets the number of notifications that wait to be raised. It is for tests.</summary>
        internal int WaitingCount => Volatile.Read(ref this._waiting);

        /// <summary>Gets or sets what is called while a notification is admitted and before it is queued. It is for tests: another producer can be held up there.</summary>
        internal Action? BeforeInsert { get; set; }

        /// <summary>Queues a notification. Once the notifier is stopped nothing is queued.</summary>
        public void Enqueue(Action raise)
        {
            var overflowed = false;
            var backlog = false;

            // Admission, the choice of the notification that overflows, and the insertion are one step: the recovery is queued after everything that was
            // admitted, never before it, so the application does not get a notification after the recovery that the recovery covered.
            lock (this._gate)
            {
                if (this._stopped)
                {
                    return;
                }

                // What is dropped is covered by the recovery, which is queued after everything that waits.
                if (this._overflowed)
                {
                    return;
                }

                var waiting = Interlocked.Increment(ref this._waiting);
                if (waiting > this._capacity)
                {
                    this._overflowed = true;
                    overflowed = true;
                    raise = this._recovery;
                }
                else if (waiting == BacklogWarning)
                {
                    backlog = true;
                }

                this.BeforeInsert?.Invoke();
                this._queue.Enqueue(raise);
            }

            if (overflowed)
            {
                this._logger.LogWarning(
                    "More than {Capacity} notifications of the studio model are waiting for the handlers of the application. The new ones are dropped until the handlers have caught up, and then Resynchronized is raised: read again what you hold.",
                    this._capacity);
            }
            else if (backlog)
            {
                this._logger.LogWarning(
                    "{Count} notifications of the studio model are waiting for the handlers of the application, which are slower than OBS Studio is. A handler that waits for something holds up all the notifications behind it.",
                    BacklogWarning);
            }

            if (Interlocked.CompareExchange(ref this._delivering, 1, 0) == 0)
            {
                _ = Task.Run(this.Deliver);
            }
        }

        /// <summary>
        /// Stops raising. The notifications that are waiting are dropped, and let go of: what they hold (the changes that they tell of, the collections that were
        /// replaced) is not kept for as long as something refers to the studio. What was posted to a context counts until the context runs it.
        /// </summary>
        public void Stop()
        {
            lock (this._gate)
            {
                this._stopped = true;
                while (this._queue.TryDequeue(out _))
                {
                    Interlocked.Decrement(ref this._waiting);
                }
            }
        }

        private void Deliver()
        {
            while (true)
            {
                while (!this._stopped && this._queue.TryDequeue(out var raise))
                {
                    if (this._context is null)
                    {
                        Interlocked.Decrement(ref this._waiting);
                        this.RunUnlessStopped(raise);
                    }
                    else
                    {
                        this.PostToContext(raise);
                    }
                }

                // Interlocked, for the full fence: a notification that was added after the last look at the queue found this task still
                // delivering and started none, so the queue has to be looked at again after the flag was reset.
                Interlocked.Exchange(ref this._delivering, 0);
                if (this._stopped || this._queue.IsEmpty || Interlocked.CompareExchange(ref this._delivering, 1, 0) != 0)
                {
                    return;
                }
            }
        }

        // What the context has not run yet when the notifier is stopped is not raised: the application may be tearing down what the handlers use. A context
        // that cannot be posted to (it has been closed) must not stop the delivery for good: what could not be posted is dropped, and counted as no longer waiting.
        // When that was the recovery, notifications are accepted again, as they are when it runs: nothing would run it.
        private void PostToContext(Action raise)
        {
            try
            {
                this._context!.Post(_ => this.RunPosted(raise), null);
                this._postFailed = false;
            }
            catch (Exception ex)
            {
                Interlocked.Decrement(ref this._waiting);
                if (ReferenceEquals(raise, this._recovery))
                {
                    lock (this._gate)
                    {
                        this._overflowed = false;
                    }
                }

                if (!this._postFailed)
                {
                    this._postFailed = true;
                    this._logger.LogError(ex, "The context for the notifications of the studio model could not be posted to. The notifications that it cannot take are dropped.");
                }
            }
        }

        // Notifications are accepted again from the moment that the application is told: what it reads then is newer than what was dropped.
        private void Recover()
        {
            lock (this._gate)
            {
                this._overflowed = false;
            }

            this._recover();
        }

        private void RunPosted(Action raise)
        {
            Interlocked.Decrement(ref this._waiting);
            this.RunUnlessStopped(raise);
        }

        private void RunUnlessStopped(Action raise)
        {
            if (!this._stopped)
            {
                this.Run(raise);
            }
        }

        private void Run(Action raise)
        {
            try
            {
                raise();
            }
            catch (Exception ex)
            {
                // The notifications isolate the handlers of the application themselves; this keeps the delivery alive whatever happens.
                this._logger.LogError(ex, "Exception while raising a notification of the studio model");
            }
        }
    }
}
