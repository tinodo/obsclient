namespace OBSStudioClient.Tests.Studio
{
    using System.Collections.Concurrent;

    /// <summary>
    /// A context that keeps what is posted to it until the test runs it: a user interface thread that is busy.
    /// </summary>
    internal sealed class HeldContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _held = new();

        public int Count => this._held.Count;

        public override void Post(SendOrPostCallback d, object? state)
        {
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
