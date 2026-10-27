namespace OBSStudioClient.Tests.TestSupport
{
    /// <summary>
    /// Captures the next invocation of an <see cref="EventHandler{TEventArgs}"/> as an awaitable task, so tests
    /// can assert that a specific <see cref="ObsClient"/> event fired without racy polling.
    /// </summary>
    internal sealed class EventAwaiter<TEventArgs>
    {
        private readonly TaskCompletionSource<TEventArgs> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public EventHandler<TEventArgs> Handler { get; }

        public EventAwaiter()
        {
            Handler = (_, e) => _tcs.TrySetResult(e);
        }

        public Task<TEventArgs> Task => _tcs.Task;
    }

    /// <summary>
    /// Same as <see cref="EventAwaiter{TEventArgs}"/>, for the plain (non-generic) <see cref="EventHandler"/>
    /// events raised for OBS events that carry no data, like <c>ExitStarted</c>.
    /// </summary>
    internal sealed class EventAwaiter
    {
        private readonly TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public EventHandler Handler { get; }

        public EventAwaiter()
        {
            Handler = (_, _) => _tcs.TrySetResult();
        }

        public Task Task => _tcs.Task;
    }
}
