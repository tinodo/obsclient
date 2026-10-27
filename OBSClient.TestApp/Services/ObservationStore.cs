using System.Text.Json;
using OBSStudioClient.TestApp.Analysis;

namespace OBSStudioClient.TestApp.Services;

/// <summary>
/// Remembers the shape of everything OBS Studio sent during the session: for each response and each event, which members
/// it had and what kind of value each was. What was seen is a sample, not a specification: a member that was not seen
/// may still exist, and one that was always there may be missing in another situation.
/// </summary>
internal sealed class ObservationStore
{
    private readonly object _gate = new();
    private readonly SortedDictionary<string, Observed> _responses = new(StringComparer.Ordinal);
    private readonly SortedDictionary<string, Observed> _events = new(StringComparer.Ordinal);

    /// <summary>Gets the number of kinds of response and event that were observed.</summary>
    public int Count
    {
        get
        {
            lock (this._gate)
            {
                return this._responses.Count + this._events.Count;
            }
        }
    }

    /// <summary>Records the response data of a request.</summary>
    public void ObserveResponse(string requestType, JsonElement responseData) => this.Observe(this._responses, requestType, responseData);

    /// <summary>Records the event data of an event.</summary>
    public void ObserveEvent(string eventType, JsonElement eventData) => this.Observe(this._events, eventType, eventData);

    /// <summary>Forgets everything that was observed.</summary>
    public void Clear()
    {
        lock (this._gate)
        {
            this._responses.Clear();
            this._events.Clear();
        }
    }

    /// <summary>Writes what was observed as JSON.</summary>
    /// <param name="obsVersion">The version of OBS Studio.</param>
    /// <param name="obsWebSocketVersion">The version of obs-websocket.</param>
    public string ToJson(string? obsVersion, string? obsWebSocketVersion)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("tool", "OBSClient.TestApp");
            writer.WriteString("note", "A sample of what OBS Studio sent, not a specification: a member that was not seen may exist, and a member that was always seen may be missing in another situation.");
            writer.WriteString("generatedAt", DateTimeOffset.Now);
            writer.WriteString("obsVersion", obsVersion);
            writer.WriteString("obsWebSocketVersion", obsWebSocketVersion);
            lock (this._gate)
            {
                Write(writer, "responses", this._responses);
                Write(writer, "events", this._events);
            }

            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void Write(Utf8JsonWriter writer, string name, SortedDictionary<string, Observed> observed)
    {
        writer.WriteStartObject(name);
        foreach (var (type, entry) in observed)
        {
            writer.WriteStartObject(type);
            writer.WriteNumber("documents", entry.Documents);
            writer.WriteStartObject("paths");
            foreach (var (path, observation) in entry.Paths)
            {
                writer.WriteStartObject(path);
                writer.WriteStartArray("types");
                foreach (var kind in observation.Types)
                {
                    writer.WriteStringValue(kind);
                }

                writer.WriteEndArray();
                writer.WriteNumber("seen", observation.Seen);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    private void Observe(SortedDictionary<string, Observed> map, string name, JsonElement data)
    {
        lock (this._gate)
        {
            if (!map.TryGetValue(name, out var entry))
            {
                entry = new Observed();
                map[name] = entry;
            }

            entry.Documents++;
            JsonShape.Observe("$", data, entry.Paths);
        }
    }

    private sealed class Observed
    {
        public int Documents { get; set; }

        public SortedDictionary<string, PathObservation> Paths { get; } = new(StringComparer.Ordinal);
    }
}
