using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;

namespace OBSStudioClient.TestApp.Services;

/// <summary>
/// The log of the app: it keeps everything that the client reports, including (at the debug level) every message that goes
/// to and from OBS Studio, and hands the entries to the screen in small batches. The messages are what the wire log of the
/// app shows, so the client has to keep logging them as "Sending: {json}" and "Received: {json}" (the unit tests check that).
/// A client logs to the <see cref="ClientLog"/> that <see cref="ForClient"/> makes for it, and not to this class.
/// </summary>
internal sealed class WireLog
{
    private const int Capacity = 20_000;

    // What the entries may hold altogether, in characters: the message of a screenshot alone is megabytes of text.
    private const long MaxCharacters = 40_000_000;
    private const int TrimBatch = 2000;
    private const string SendingPrefix = "Sending: ";
    private const string ReceivedPrefix = "Received: ";

    private readonly object _gate = new();
    private readonly List<LogEntry> _entries = [];
    private readonly List<LogEntry> _pending = [];
    private readonly ObservationStore _observations;
    private readonly DispatcherQueueTimer _timer;
    private volatile ClientLog? _clientLog;
    private long _sequence;
    private long _characters;

    /// <summary>Initializes a new instance of the <see cref="WireLog"/> class; call it on the thread of the user interface.</summary>
    /// <param name="dispatcher">The dispatcher of the user interface.</param>
    /// <param name="observations">Where the shape of what OBS Studio sends is recorded.</param>
    public WireLog(DispatcherQueue dispatcher, ObservationStore observations)
    {
        this._observations = observations;
        this._timer = dispatcher.CreateTimer();
        this._timer.Interval = TimeSpan.FromMilliseconds(150);
        this._timer.IsRepeating = true;
        this._timer.Tick += this.OnTick;
        this._timer.Start();
    }

    /// <summary>Occurs, on the thread of the user interface, when entries were added since the last time.</summary>
    public event EventHandler<IReadOnlyList<LogEntry>>? Added;

    /// <summary>Gets the position of the last entry.</summary>
    public long Sequence
    {
        get
        {
            lock (this._gate)
            {
                return this._sequence;
            }
        }
    }

    /// <summary>Gets the position of the oldest entry that the log still holds, or of the next entry when it holds none.</summary>
    public long OldestSequence
    {
        get
        {
            lock (this._gate)
            {
                return this._entries.Count > 0 ? this._entries[0].Sequence : this._sequence + 1;
            }
        }
    }

    /// <summary>Makes the logger for a client that is about to connect.</summary>
    /// <returns>The logger that the client logs to.</returns>
    /// <remarks>
    /// The app keeps this log when it connects again with a new client, and the client that it replaces can still log for a moment. The
    /// messages in which OBS Studio sent an event wait for that event in the pairing of the logger of the client that received them, so a
    /// message that a replaced client left behind is never matched with an event of the new one.
    /// </remarks>
    public ClientLog ForClient()
    {
        var log = new ClientLog(this);
        this._clientLog = log;
        return log;
    }

    /// <summary>Adds a message of the app itself to the log.</summary>
    public void Note(LogLevel level, string message) => this.Write(null, level, message, null, static (text, _) => text);

    /// <summary>Adds what a client logged to the log.</summary>
    /// <typeparam name="TState">The type of what was logged.</typeparam>
    /// <param name="pairing">Where a message in which OBS Studio sent an event waits for that event: the pairing of the client that logged it, or null.</param>
    /// <param name="logLevel">The level of the entry.</param>
    /// <param name="state">What was logged.</param>
    /// <param name="exception">The exception that was logged with it, if any.</param>
    /// <param name="formatter">Makes the text of the entry of <paramref name="state"/> and <paramref name="exception"/>.</param>
    internal void Write<TState>(EventPairing? pairing, LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (logLevel == LogLevel.None)
        {
            return;
        }

        // The client logs a message before it reads it, so an exception here would close the connection for a message that is not
        // what the protocol says. A log that fails keeps what it can: the report of the failure, in place of the entry.
        LogEntry entry;
        try
        {
            entry = this.Create(logLevel, formatter(state, exception), exception);
        }
        catch (Exception problem) when (problem is not OutOfMemoryException)
        {
            entry = new LogEntry
            {
                Time = DateTimeOffset.Now,
                Kind = LogKind.Client,
                Level = LogLevel.Error,
                Title = "The log could not make an entry",
                Text = problem.ToString(),
            };
        }

        lock (this._gate)
        {
            entry.Sequence = ++this._sequence;
            this._entries.Add(entry);
            this._pending.Add(entry);
            this._characters += entry.Text.Length;
            this.Trim();
        }

        pairing?.Received(entry);
    }

    /// <summary>Stops handing entries to the screen, for the end of the app.</summary>
    public void Stop() => this._timer.Stop();

    /// <summary>Gets a copy of the entries.</summary>
    public IReadOnlyList<LogEntry> Snapshot()
    {
        lock (this._gate)
        {
            return [.. this._entries];
        }
    }

    /// <summary>Forgets the entries.</summary>
    public void Clear()
    {
        lock (this._gate)
        {
            this._entries.Clear();
            this._pending.Clear();
            this._characters = 0;
        }

        this._clientLog?.Pairing.Clear();
    }

    /// <summary>Finds the response to a request that was sent after a given point of the log.</summary>
    /// <param name="requestType">The type of the request, as OBS Studio names it.</param>
    /// <param name="afterSequence">Only entries after this position count.</param>
    /// <returns>The last response of that type, or null.</returns>
    public LogEntry? FindResponse(string requestType, long afterSequence) =>
        this.FindLast(afterSequence, entry => entry.OpCode == 7 && entry.RequestType == requestType);

    /// <summary>Finds the response to a request batch that was sent after a given point of the log.</summary>
    /// <param name="requestId">The identifier of the batch.</param>
    /// <param name="afterSequence">Only entries after this position count.</param>
    /// <returns>The response of that batch, or null.</returns>
    public LogEntry? FindBatchResponse(string requestId, long afterSequence) =>
        this.FindLast(afterSequence, entry => entry.OpCode == 9 && entry.RequestId == requestId);

    private void ObserveResponse(string requestType, JsonElement responseData)
    {
        // A request without response data has none, or a null: that is not a shape.
        if (responseData.ValueKind != JsonValueKind.Null)
        {
            this._observations.ObserveResponse(requestType, responseData);
        }
    }

    /// <summary>Drops the oldest entries when there are too many, or when they hold too much text. Call it with the lock held.</summary>
    private void Trim()
    {
        if (this._entries.Count <= Capacity + TrimBatch && this._characters <= MaxCharacters)
        {
            return;
        }

        // Down to three quarters of what is allowed, so that the cost of removing is not paid for every entry that is added.
        var remove = 0;
        var removed = 0L;
        while (remove < this._entries.Count - 1
            && (this._entries.Count - remove > Capacity || this._characters - removed > MaxCharacters / 4 * 3))
        {
            removed += this._entries[remove].Text.Length;
            remove++;
        }

        this._entries.RemoveRange(0, remove);
        this._characters -= removed;
    }

    private LogEntry? FindLast(long afterSequence, Func<LogEntry, bool> match)
    {
        lock (this._gate)
        {
            for (var index = this._entries.Count - 1; index >= 0; index--)
            {
                var entry = this._entries[index];
                if (entry.Sequence <= afterSequence)
                {
                    break;
                }

                if (entry.Kind == LogKind.Received && match(entry))
                {
                    return entry;
                }
            }

            return null;
        }
    }

    private void OnTick(DispatcherQueueTimer sender, object args)
    {
        List<LogEntry> batch;
        lock (this._gate)
        {
            if (this._pending.Count == 0)
            {
                return;
            }

            batch = [.. this._pending];
            this._pending.Clear();
        }

        this.Added?.Invoke(this, batch);
    }

    private LogEntry Create(LogLevel level, string message, Exception? exception)
    {
        var time = DateTimeOffset.Now;
        if (message.StartsWith(SendingPrefix, StringComparison.Ordinal))
        {
            return this.CreateFrame(time, level, LogKind.Sent, message[SendingPrefix.Length..]);
        }

        if (message.StartsWith(ReceivedPrefix, StringComparison.Ordinal))
        {
            return this.CreateFrame(time, level, LogKind.Received, message[ReceivedPrefix.Length..]);
        }

        var firstLine = message.Split('\n', 2)[0].TrimEnd('\r');
        var text = exception is null ? message : message + Environment.NewLine + exception;
        return new LogEntry { Time = time, Kind = LogKind.Client, Level = level, Title = firstLine, Text = text };
    }

    private LogEntry CreateFrame(DateTimeOffset time, LogLevel level, LogKind kind, string json)
    {
        var info = FrameInfo.None;

        try
        {
            using var document = JsonDocument.Parse(json);
            info = FrameInfo.Read(document.RootElement);
            if (kind == LogKind.Received)
            {
                this.Observe(document.RootElement, info);
            }
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            // Not JSON, or not what the protocol says: the entry is kept as text, with what could be read of it.
        }

        // The whole message: what is shown on screen leaves out a very long string (an image), what is exported and compared does not.
        return new LogEntry
        {
            Time = time,
            Kind = kind,
            Level = level,
            Title = info.Title,
            Text = json,
            OpCode = info.OpCode,
            RequestType = info.RequestType,
            RequestId = info.RequestId,
            EventType = info.EventType,
            Failed = info.Failed,
        };
    }

    private void Observe(JsonElement root, FrameInfo info)
    {
        if (!root.TryGetProperty("d", out var data) || data.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (info.OpCode == 7 && info.RequestType is not null && data.TryGetProperty("responseData", out var responseData))
        {
            this.ObserveResponse(info.RequestType, responseData);
        }
        else if (info.OpCode == 9 && data.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
        {
            // The responses to the requests of a batch are in the answer to the batch.
            foreach (var result in results.EnumerateArray())
            {
                if (result.ValueKind == JsonValueKind.Object
                    && FrameInfo.StringOf(result, "requestType") is { } resultType
                    && result.TryGetProperty("responseData", out var resultData))
                {
                    this.ObserveResponse(resultType, resultData);
                }
            }
        }
        else if (info.OpCode == 5 && info.EventType is not null && data.TryGetProperty("eventData", out var eventData))
        {
            this._observations.ObserveEvent(info.EventType, eventData);
        }
    }
}
