namespace OBSStudioClient.Studio
{
    using System.Collections.Concurrent;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Raises the notifications of an <see cref="ObsStudio"/> one at a time, in the order in which they were queued, outside the thread that runs
    /// the model: what a handler does, or how long it takes, cannot disturb the model. A handler that waits holds up only the notifications behind it.
    /// </summary>
    /// <remarks>
    /// Like the notifications of the <see cref="ObsClient"/>: there is no task that waits for work; a task is started when something is queued
    /// and nothing is being delivered, and it ends when the queue is empty. With a <see cref="SynchronizationContext"/> the notifications are posted to it
    /// in order, and what it does with them (a user interface thread runs them one by one) is its business.
    /// </remarks>
    internal sealed class StudioNotifier
    {
        private const int BacklogWarning = 1000;

        private readonly ConcurrentQueue<Action> _queue = new();
        private readonly SynchronizationContext? _context;
        private readonly ILogger _logger;
        private int _waiting;
        private int _delivering;
        private volatile bool _stopped;

        public StudioNotifier(SynchronizationContext? context, ILogger logger)
        {
            this._context = context;
            this._logger = logger;
        }

        /// <summary>Queues a notification. Once the notifier is stopped nothing is queued.</summary>
        public void Enqueue(Action raise)
        {
            if (this._stopped)
            {
                return;
            }

            if (Interlocked.Increment(ref this._waiting) == BacklogWarning)
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
                    Interlocked.Decrement(ref this._waiting);
                    if (this._context is null)
                    {
                        this.Run(raise);
                    }
                    else
                    {
                        // What the context has not run yet when the notifier is stopped is not raised: the application may be tearing down what the handlers use.
                        this._context.Post(_ => this.RunUnlessStopped(raise), null);
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
