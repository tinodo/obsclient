using Microsoft.Extensions.Logging;

namespace OBSStudioClient.TestApp.Services;

/// <summary>
/// The logger of one client. What a client logs goes to the log of the app, which outlives it: the app connects again with a new
/// client, and the one that was replaced can still log, and raise an event that it had under way, for a moment. The messages in
/// which OBS Studio sent an event wait for that event in a pairing that belongs to this client alone, so a message that a replaced
/// client left behind cannot be matched with an event of the client that replaced it.
/// </summary>
internal sealed class ClientLog : ILogger<ObsClient>
{
    private readonly WireLog _log;

    /// <summary>Initializes a new instance of the <see cref="ClientLog"/> class.</summary>
    /// <param name="log">The log of the app, which this logger writes to.</param>
    public ClientLog(WireLog log)
    {
        this._log = log;
    }

    /// <summary>Gets what keeps the messages of the events that this client received, until the client raises the events.</summary>
    public EventPairing Pairing { get; } = new();

    /// <inheritdoc/>
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    /// <inheritdoc/>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        this._log.Write(this.Pairing, logLevel, state, exception, formatter);

    /// <summary>Finds the message in which OBS Studio sent the event that the client has just raised, and takes it from the pairing.</summary>
    /// <param name="eventType">The type of the event.</param>
    /// <param name="typedJson">What the client made of the event: the event arguments, as JSON.</param>
    /// <returns>The message of that event, or null.</returns>
    /// <remarks>
    /// The client raises the events on a thread of its own, one at a time and in the order in which they arrived, so a few more messages of
    /// that type may have arrived since: the message of the event is the oldest one that was not matched with an event yet.
    /// </remarks>
    public LogEntry? ClaimEvent(string eventType, string typedJson) => this.Pairing.Match(eventType, typedJson);
}
