namespace OBSStudioClient.Tests.TestSupport
{
    /// <summary>
    /// Small async test helpers shared across the suite: bounding an awaitable with a hard timeout, and
    /// polling for a condition to become true.
    /// </summary>
    internal static class AsyncTestHelpers
    {
        public static async Task<T> WithTimeoutAsync<T>(this Task<T> task, TimeSpan timeout, string? message = null)
        {
            var delay = Task.Delay(timeout);
            var completed = await Task.WhenAny(task, delay).ConfigureAwait(false);
            if (completed == delay)
            {
                throw new TimeoutException(message ?? $"Operation timed out after {timeout}.");
            }

            return await task.ConfigureAwait(false);
        }

        public static async Task WithTimeoutAsync(this Task task, TimeSpan timeout, string? message = null)
        {
            var delay = Task.Delay(timeout);
            var completed = await Task.WhenAny(task, delay).ConfigureAwait(false);
            if (completed == delay)
            {
                throw new TimeoutException(message ?? $"Operation timed out after {timeout}.");
            }

            await task.ConfigureAwait(false);
        }

        /// <summary>
        /// Polls <paramref name="condition"/> until it returns <see langword="true"/> or <paramref name="timeout"/> elapses.
        /// </summary>
        /// <returns><see langword="true"/> if the condition became true in time; otherwise <see langword="false"/>.</returns>
        public static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout, TimeSpan? pollInterval = null)
        {
            var interval = pollInterval ?? TimeSpan.FromMilliseconds(25);
            using var cts = new CancellationTokenSource(timeout);
            while (!condition())
            {
                try
                {
                    await Task.Delay(interval, cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return condition();
                }
            }

            return true;
        }
    }
}
