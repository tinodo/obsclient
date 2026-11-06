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
        private readonly SynchronizationContext? _context;
        private readonly ILogger _logger;
        private readonly int _capacity;
        private readonly Action _recover;
        private int _waiting;
        private int _delivering;
        private int _overflowed;
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
        }

        /// <summary>Gets the number of notifications that wait to be raised. It is for tests.</summary>
        internal int WaitingCount => Volatile.Read(ref this._waiting);

        /// <summary>Queues a notification. Once the notifier is stopped nothing is queued.</summary>
        public void Enqueue(Action raise)
        {
            if (this._stopped)
            {
                return;
            }

            // What is dropped is covered by the recovery, which is queued after everything that waits.
            if (Volatile.Read(ref this._overflowed) != 0)
            {
                return;
            }

            var waiting = Interlocked.Increment(ref this._waiting);
            if (waiting > this._capacity)
            {
                if (Interlocked.CompareExchange(ref this._overflowed, 1, 0) != 0)
                {
                    Interlocked.Decrement(ref this._waiting);
                    return;
                }

                this._logger.LogWarning(
                    "More than {Capacity} notifications of the studio model are waiting for the handlers of the application. The new ones are dropped until the handlers have caught up, and then Resynchronized is raised: read again what you hold.",
                    this._capacity);
                raise = this.Recover;
            }
            else if (waiting == BacklogWarning)
            {
                this._logger.LogWarning(
                    "{Count} notifications of the studio model are waiting for the handlers of the application, which are slower than OBS Studio is. A handler that waits for something holds up all the notifications behind it.",
                    BacklogWarning);
            }

            this._queue.Enqueue(raise);
            if (Interlocked.CompareExchange(ref this._delivering, 1, 0) == 0)
            {
                _ = Task.Run(this.Deliver);
            }
        }

        /// <summary>Stops raising: the notifications that are waiting are dropped.</summary>
        public void Stop()
        {
            this._stopped = true;
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
                        this.Run(raise);
                    }
                    else
                    {
                        // What the context has not run yet when the notifier is stopped is not raised: the application may be tearing down what the handlers use.
                        this._context.Post(_ => this.RunPosted(raise), null);
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

        // Notifications are accepted again from the moment that the application is told: what it reads then is newer than what was dropped.
        private void Recover()
        {
            Volatile.Write(ref this._overflowed, 0);
            this._recover();
        }

        private void RunPosted(Action raise)
        {
            Interlocked.Decrement(ref this._waiting);
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
