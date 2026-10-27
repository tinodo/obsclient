using System.Globalization;
using Microsoft.Extensions.Logging;

namespace OBSStudioClient.TestApp.Services;

/// <summary>Where an entry of the log comes from.</summary>
public enum LogKind
{
    /// <summary>A message of the client's own logging.</summary>
    Client,

    /// <summary>A message that was sent to OBS Studio.</summary>
    Sent,

    /// <summary>A message that was received from OBS Studio.</summary>
    Received,
}

/// <summary>One line of the log: a message that went over the wire, or something the client reported.</summary>
public sealed class LogEntry
{
    private string? _displayText;

    /// <summary>Gets the position of the entry in the log; it grows with every entry.</summary>
    public long Sequence { get; internal set; }

    /// <summary>Gets the time the entry was made.</summary>
    public DateTimeOffset Time { get; internal set; }

    /// <summary>Gets where the entry comes from.</summary>
    public LogKind Kind { get; internal set; }

    /// <summary>Gets the level of the entry.</summary>
    public LogLevel Level { get; internal set; }

    /// <summary>Gets what the entry is, in a few words.</summary>
    public string Title { get; internal set; } = string.Empty;

    /// <summary>Gets the whole entry: the JSON of a message exactly as it went over the wire, or the text of the report.</summary>
    public string Text { get; internal set; } = string.Empty;

    /// <summary>
    /// Gets the entry as it is shown on screen: in a message, a very long string (an image, for example) is left out, so that the
    /// rest stays readable. <see cref="Text"/> is the whole entry, which is what is compared and exported.
    /// </summary>
    public string DisplayText => this._displayText ??= this.Kind == LogKind.Client ? this.Text : JsonTools.Collapse(this.Text);

    /// <summary>Gets the operation code of a message.</summary>
    public int? OpCode { get; internal set; }

    /// <summary>Gets the type of the request that a message is about.</summary>
    public string? RequestType { get; internal set; }

    /// <summary>Gets the identifier of the request that a message is about.</summary>
    public string? RequestId { get; internal set; }

    /// <summary>Gets the type of the event that a message carries.</summary>
    public string? EventType { get; internal set; }

    /// <summary>Gets a value indicating whether the message is the response to a request that failed, or to a batch in which a request failed.</summary>
    public bool Failed { get; internal set; }

    /// <summary>Gets a value indicating whether the entry deserves attention.</summary>
    public bool IsProblem => this.Failed || this.Level >= LogLevel.Warning;

    /// <summary>Gets the time, for the list.</summary>
    public string TimeText => this.Time.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);

    /// <summary>Gets the direction of a message, for the list.</summary>
    public string Direction => this.Kind switch
    {
        LogKind.Sent => "\u2192",
        LogKind.Received => "\u2190",
        _ => "\u2022",
    };

    /// <summary>Gets the level, for the list.</summary>
    public string LevelText => this.Level switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        LogLevel.Critical => "CRT",
        _ => string.Empty,
    };
}

/// <summary>An event of OBS Studio, as the client raised it.</summary>
public sealed class SessionEvent
{
    /// <summary>Initializes a new instance of the <see cref="SessionEvent"/> class.</summary>
    /// <param name="time">The time the event was raised.</param>
    /// <param name="name">The name of the event.</param>
    /// <param name="category">The category of the protocol documentation.</param>
    /// <param name="typedJson">What the client made of the event: the event arguments, as JSON.</param>
    /// <param name="rawJson">What OBS Studio sent, when it is known.</param>
    public SessionEvent(DateTimeOffset time, string name, string category, string typedJson, string? rawJson)
    {
        this.Time = time;
        this.Name = name;
        this.Category = category;
        this.TypedJson = typedJson;
        this.RawJson = rawJson;
    }

    /// <summary>Gets the time the event was raised.</summary>
    public DateTimeOffset Time { get; }

    /// <summary>Gets the name of the event.</summary>
    public string Name { get; }

    /// <summary>Gets the category of the protocol documentation.</summary>
    public string Category { get; }

    /// <summary>Gets what the client made of the event: the event arguments, as JSON.</summary>
    public string TypedJson { get; }

    /// <summary>Gets what OBS Studio sent, when it is known.</summary>
    public string? RawJson { get; }

    /// <summary>Gets the time, for the list.</summary>
    public string TimeText => this.Time.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);

    /// <summary>Gets the data of the event on one line, for the list.</summary>
    public string Summary
    {
        get
        {
            var line = string.Join(' ', this.TypedJson.Split(['\r', '\n', ' '], StringSplitOptions.RemoveEmptyEntries));
            return line.Length <= 140 ? line : line[..140] + "\u2026";
        }
    }
}
