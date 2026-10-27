using System.Globalization;
using System.Text.Json;

namespace OBSStudioClient.TestApp.Services;

/// <summary>
/// What a message of the protocol is: its operation, and the request or the event that it is about. A message is read
/// defensively: the log has to keep, and show, a message that is not what the protocol says (a code that is a string, a status
/// that is a number), so that what is wrong with it can be seen.
/// </summary>
/// <param name="OpCode">The operation code, when the message has one.</param>
/// <param name="RequestType">The type of the request that the message is about.</param>
/// <param name="RequestId">The identifier of the request that the message is about.</param>
/// <param name="EventType">The type of the event that the message carries.</param>
/// <param name="Failed">Whether the message is the response to a request that failed, or to a batch in which a request failed.</param>
/// <param name="Status">What went wrong, for the title of a response that failed.</param>
internal sealed record FrameInfo(int? OpCode, string? RequestType, string? RequestId, string? EventType, bool Failed, string Status)
{
    /// <summary>Gets what is known of a message that could not be read.</summary>
    public static FrameInfo None { get; } = new(null, null, null, null, false, string.Empty);

    /// <summary>Gets what the message is, in a few words.</summary>
    public string Title => this.OpCode switch
    {
        0 => "Hello",
        1 => "Identify",
        2 => "Identified",
        3 => "Reidentify",
        5 => $"Event {this.EventType}",
        6 => $"Request {this.RequestType}",
        7 => $"Response {this.RequestType}{this.Status}",
        8 => "Request batch",
        9 => $"Request batch response{this.Status}",
        _ => "Message",
    };

    /// <summary>Reads what a message is; text that is not JSON gives <see cref="None"/>.</summary>
    /// <param name="json">The message.</param>
    /// <returns>What the message is.</returns>
    public static FrameInfo Read(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return Read(document.RootElement);
        }
        catch (JsonException)
        {
            return None;
        }
    }

    /// <summary>Reads what a message is.</summary>
    /// <param name="root">The message.</param>
    /// <returns>What the message is.</returns>
    public static FrameInfo Read(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return None;
        }

        var op = root.TryGetProperty("op", out var opElement) ? IntOf(opElement) : null;
        if (!root.TryGetProperty("d", out var data) || data.ValueKind != JsonValueKind.Object)
        {
            return None with { OpCode = op };
        }

        var failed = false;
        var status = string.Empty;
        if (op == 7 && data.TryGetProperty("requestStatus", out var requestStatus) && requestStatus.ValueKind == JsonValueKind.Object)
        {
            failed = requestStatus.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.False;
            if (failed)
            {
                var code = requestStatus.TryGetProperty("code", out var codeElement) && IntOf(codeElement) is { } value ? value.ToString(CultureInfo.InvariantCulture) : "?";
                var comment = StringOf(requestStatus, "comment");
                status = $" failed ({code}){(string.IsNullOrEmpty(comment) ? string.Empty : ": " + comment)}";
            }
        }
        else if (op == 9 && data.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
        {
            // A batch is answered with a message that succeeded; what failed is in its results.
            var failures = CountFailures(results, out var total);
            if (failures > 0)
            {
                failed = true;
                status = $" ({failures} of {total} failed)";
            }
        }

        return new FrameInfo(op, StringOf(data, "requestType"), StringOf(data, "requestId"), StringOf(data, "eventType"), failed, status);
    }

    /// <summary>Reads a member that is a string; anything else is null.</summary>
    /// <param name="data">The object that holds the member.</param>
    /// <param name="name">The name of the member.</param>
    /// <returns>The string, or null.</returns>
    public static string? StringOf(JsonElement data, string name) =>
        data.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static int? IntOf(JsonElement element) =>
        element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var value) ? value : null;

    // The results of a batch are read as defensively as the rest: only a status whose result is the JSON value false counts.
    private static int CountFailures(JsonElement results, out int total)
    {
        total = 0;
        var failures = 0;
        foreach (var item in results.EnumerateArray())
        {
            total++;
            if (item.ValueKind == JsonValueKind.Object
                && item.TryGetProperty("requestStatus", out var itemStatus)
                && itemStatus.ValueKind == JsonValueKind.Object
                && itemStatus.TryGetProperty("result", out var itemResult)
                && itemResult.ValueKind == JsonValueKind.False)
            {
                failures++;
            }
        }

        return failures;
    }
}
