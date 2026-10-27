using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;

namespace OBSStudioClient.TestApp.Services;

/// <summary>
/// The logger of the client: it keeps everything the client reports, including (at the debug level) every message that goes
/// to and from OBS Studio, and hands the entries to the screen in small batches. The messages are what the wire log of the
/// app shows, so the client has to keep logging them as "Sending: {json}" and "Received: {json}" (the unit tests check that).
/// </summary>
internal sealed class WireLog : ILogger<ObsClient>
{
    private const int Capacity = 20_000;
    private const string SendingPrefix = "Sending: ";
    private const string ReceivedPrefix = "Received: ";

    private readonly object _gate = new();
    private readonly List<LogEntry> _entries = [];
    private readonly List<LogEntry> _pending = [];
    private readonly ObservationStore _observations;
    private readonly DispatcherQueueTimer _timer;
    private long _sequence;

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

    /// <inheritdoc/>
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    /// <inheritdoc/>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!this.IsEnabled(logLevel))
        {
            return;
        }

        var entry = this.Create(logLevel, formatter(state, exception), exception);
        lock (this._gate)
        {
            entry.Sequence = ++this._sequence;
            this._entries.Add(entry);
            this._pending.Add(entry);
            if (this._entries.Count > Capacity + 2000)
            {
                this._entries.RemoveRange(0, 2000);
            }
        }
    }

    /// <summary>Adds a message of the app itself to the log.</summary>
    public void Note(LogLevel level, string message) => this.Log(level, default, message, null, static (text, _) => text);

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
        }
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

    /// <summary>Finds the message in which OBS Studio sent the event that the client has just raised.</summary>
    /// <param name="eventType">The type of the event.</param>
    /// <returns>The latest message of that event, or null.</returns>
    public LogEntry? LatestEvent(string eventType)
    {
        lock (this._gate)
        {
            for (var index = this._entries.Count - 1; index >= Math.Max(0, this._entries.Count - 500); index--)
            {
                var entry = this._entries[index];
                if (entry.Kind == LogKind.Received && entry.OpCode == 5 && entry.EventType == eventType)
                {
                    return entry;
                }
            }

            return null;
        }
    }

    private static string? StringOf(JsonElement data, string name) =>
        data.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

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
        int? op = null;
        string? requestType = null;
        string? requestId = null;
        string? eventType = null;
        var failed = false;
        var status = string.Empty;

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("op", out var opElement) && opElement.TryGetInt32(out var opValue))
                {
                    op = opValue;
                }

                if (root.TryGetProperty("d", out var data) && data.ValueKind == JsonValueKind.Object)
                {
                    requestType = StringOf(data, "requestType");
                    requestId = StringOf(data, "requestId");
                    eventType = StringOf(data, "eventType");

                    if (op == 7 && data.TryGetProperty("requestStatus", out var requestStatus) && requestStatus.ValueKind == JsonValueKind.Object)
                    {
                        failed = requestStatus.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.False;
                        if (failed)
                        {
                            var code = requestStatus.TryGetProperty("code", out var codeElement) && codeElement.TryGetInt32(out var codeValue) ? codeValue.ToString() : "?";
                            var comment = StringOf(requestStatus, "comment");
                            status = $" failed ({code}){(string.IsNullOrEmpty(comment) ? string.Empty : ": " + comment)}";
                        }
                    }

                    if (kind == LogKind.Received)
                    {
                        if (op == 7 && requestType is not null && data.TryGetProperty("responseData", out var responseData))
                        {
                            this._observations.ObserveResponse(requestType, responseData);
                        }
                        else if (op == 5 && eventType is not null && data.TryGetProperty("eventData", out var eventData))
                        {
                            this._observations.ObserveEvent(eventType, eventData);
                        }
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Not JSON: the entry is kept as text.
        }

        var title = op switch
        {
            0 => "Hello",
            1 => "Identify",
            2 => "Identified",
            3 => "Reidentify",
            5 => $"Event {eventType}",
            6 => $"Request {requestType}",
            7 => $"Response {requestType}{status}",
            8 => "Request batch",
            9 => "Request batch response",
            _ => "Message",
        };

        return new LogEntry
        {
            Time = time,
            Kind = kind,
            Level = level,
            Title = title,
            Text = JsonTools.Collapse(json),
            OpCode = op,
            RequestType = requestType,
            RequestId = requestId,
            EventType = eventType,
            Failed = failed,
        };
    }
}
