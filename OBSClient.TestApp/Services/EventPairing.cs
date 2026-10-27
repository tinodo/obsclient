using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace OBSStudioClient.TestApp.Services;

/// <summary>
/// Finds the message in which OBS Studio sent an event that the client has raised. The client hands the events to a thread of its own,
/// so it raises an event a little after the message was logged, and the message of an event is not the latest of its type: it is the
/// oldest one that no event was matched with yet. A message that the client skipped (one that it could not read) must not put every
/// later pair off by one, so when there is a choice, the message whose members agree best with what the client made of the event is
/// taken, and the ones before it are dropped.
/// </summary>
internal sealed class EventPairing
{
    // A type of event that nobody asks for must not make the pairing grow without end.
    private const int MaxWaiting = 256;

    // The volume meters are the one event that the client thins out (it passes on one in every 100 ms and skips the rest), so the
    // messages that wait for them are not in step with the events: the latest message is taken.
    private const string VolumeMeters = "InputVolumeMeters";

    private readonly object _gate = new();
    private readonly Dictionary<string, List<LogEntry>> _waiting = new(StringComparer.Ordinal);

    /// <summary>Takes note of a message that was received. A message that is not an event is ignored.</summary>
    /// <param name="entry">The entry of the log.</param>
    public void Received(LogEntry entry)
    {
        if (entry.Kind != LogKind.Received || entry.OpCode != 5 || entry.EventType is not { } eventType)
        {
            return;
        }

        lock (this._gate)
        {
            if (!this._waiting.TryGetValue(eventType, out var list))
            {
                list = [];
                this._waiting[eventType] = list;
            }

            list.Add(entry);
            if (list.Count > MaxWaiting)
            {
                list.RemoveAt(0);
            }
        }
    }

    /// <summary>Finds the message of an event that the client has just raised, and takes it from the pairing.</summary>
    /// <param name="eventType">The type of the event.</param>
    /// <param name="typedJson">What the client made of the event: the event arguments, as JSON.</param>
    /// <returns>The message, or null when none is waiting.</returns>
    public LogEntry? Match(string eventType, string typedJson)
    {
        lock (this._gate)
        {
            if (!this._waiting.TryGetValue(eventType, out var list) || list.Count == 0)
            {
                return null;
            }

            if (eventType == VolumeMeters)
            {
                var latest = list[^1];
                list.Clear();
                return latest;
            }

            var chosen = 0;
            if (list.Count > 1 && TryParse(typedJson, out var typed))
            {
                using (typed)
                {
                    var best = int.MinValue;
                    for (var index = 0; index < list.Count; index++)
                    {
                        var score = Agreement(list[index].Text, typed.RootElement);
                        if (score > best)
                        {
                            best = score;
                            chosen = index;
                        }
                    }
                }
            }

            var match = list[chosen];
            list.RemoveRange(0, chosen + 1);
            return match;
        }
    }

    /// <summary>Forgets the messages that are waiting.</summary>
    public void Clear()
    {
        lock (this._gate)
        {
            this._waiting.Clear();
        }
    }

    private static bool TryParse(string json, [NotNullWhen(true)] out JsonDocument? document)
    {
        try
        {
            document = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            // The app shows a sentence instead of JSON when it could not show the arguments of the event.
            document = null;
            return false;
        }
    }

    // How many of the plain members (a string, a number, a boolean) of the event data that the message and the typed event have in
    // common are equal, less how many differ. A member that is a value of another kind in one of them (the name of an enumeration
    // for a number) does not count.
    private static int Agreement(string frame, JsonElement typed)
    {
        if (typed.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        try
        {
            using var document = JsonDocument.Parse(frame);
            if (!document.RootElement.TryGetProperty("d", out var data) || data.ValueKind != JsonValueKind.Object
                || !data.TryGetProperty("eventData", out var raw) || raw.ValueKind != JsonValueKind.Object)
            {
                return 0;
            }

            var score = 0;
            foreach (var member in raw.EnumerateObject())
            {
                if (Find(typed, member.Name) is { } other && Same(member.Value, other) is { } equal)
                {
                    score += equal ? 1 : -1;
                }
            }

            return score;
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    private static JsonElement? Find(JsonElement element, string name)
    {
        foreach (var member in element.EnumerateObject())
        {
            if (string.Equals(member.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return member.Value;
            }
        }

        return null;
    }

    private static bool? Same(JsonElement left, JsonElement right)
    {
        if (left.ValueKind == JsonValueKind.String && right.ValueKind == JsonValueKind.String)
        {
            return string.Equals(left.GetString(), right.GetString(), StringComparison.Ordinal);
        }

        if (left.ValueKind == JsonValueKind.Number && right.ValueKind == JsonValueKind.Number
            && left.TryGetDouble(out var a) && right.TryGetDouble(out var b))
        {
            return Math.Abs(a - b) <= 1e-6 * Math.Max(1, Math.Max(Math.Abs(a), Math.Abs(b)));
        }

        if ((left.ValueKind is JsonValueKind.True or JsonValueKind.False) && (right.ValueKind is JsonValueKind.True or JsonValueKind.False))
        {
            return left.ValueKind == right.ValueKind;
        }

        return null;
    }
}
