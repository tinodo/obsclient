namespace OBSStudioClient.Tests.Studio
{
    using System.Collections.Concurrent;

    /// <summary>
    /// A context that keeps what is posted to it until the test runs it: a user interface thread that is busy. It can also refuse what is posted, as a context
    /// does that has been closed.
    /// </summary>
    internal sealed class HeldContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _held = new();

        public int Count => this._held.Count;

        /// <summary>Gets or sets whether posting throws, as it does to a context that has been closed.</summary>
        public volatile bool Failing;

        public override void Post(SendOrPostCallback d, object? state)
        {
            if (this.Failing)
            {
                throw new InvalidOperationException("The context is closed.");
            }

            this._held.Enqueue((d, state));
        }

        public void RunAll()
        {
            while (this._held.TryDequeue(out var held))
            {
                held.Callback(held.State);
            }
        }
    }
}
