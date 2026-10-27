namespace OBSStudioClient.Tests.TestSupport
{
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// An <see cref="ILogger{TCategoryName}"/> that keeps what it is given, for the tests that check what the client logs.
    /// </summary>
    internal sealed class ListLogger : ILogger<ObsClient>
    {
        private readonly List<(LogLevel Level, string Message)> _entries = [];

        /// <summary>
        /// Gets a copy of the entries so far.
        /// </summary>
        public IReadOnlyList<(LogLevel Level, string Message)> Entries
        {
            get
            {
                lock (this._entries)
                {
                    return [.. this._entries];
                }
            }
        }

        /// <inheritdoc/>
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        /// <inheritdoc/>
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <inheritdoc/>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (this._entries)
            {
                this._entries.Add((logLevel, formatter(state, exception)));
            }
        }
    }
}
