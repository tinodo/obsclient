using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace OBSStudioClient.TestApp.Services;

/// <summary>Reading, writing and cleaning up JSON for the screen and for exports.</summary>
internal static class JsonTools
{
    private static readonly Regex LongString = new("\"[^\"]{2000,}\"", RegexOptions.Compiled);

    // The value of a member whose name ends in password, secret, token or key (streamKey, apiKey, ...) or is authentication.
    // The value is read as a JSON string, so that a secret that contains an escaped quote is hidden as a whole.
    private static readonly Regex Secret = new(
        @"(""(?:[^""\\]*(?:password|secret|token|key)|authentication)""\s*:\s*)""(?:[^""\\]|\\.)*""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly JsonWriterOptions IndentedWriter = new() { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Settings to show a value to a person: indented, and with the names of enumerations.</summary>
    public static JsonSerializerOptions Display { get; } = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Settings to compare a value with what OBS Studio sent: nothing that the client does not do itself.</summary>
    public static JsonSerializerOptions Plain { get; } = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    /// <summary>Shows a value the way a call of the client returned it.</summary>
    public static string Serialize(object? value) =>
        value is null ? "null" : Collapse(JsonSerializer.Serialize(value, value.GetType(), Display));

    /// <summary>Turns a value into JSON, for comparison with what OBS Studio sent.</summary>
    public static JsonDocument? ToDocument(object? value) =>
        value is null ? null : JsonSerializer.SerializeToDocument(value, value.GetType(), Plain);

    /// <summary>Indents JSON; text that is not JSON is returned as it is.</summary>
    public static string Pretty(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, IndentedWriter))
            {
                document.WriteTo(writer);
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }
        catch (JsonException)
        {
            return json;
        }
    }

    /// <summary>Replaces every very long string (an image, for example) by a note of its length, so that the rest stays readable.</summary>
    public static string Collapse(string text) =>
        text.Length < 2000 ? text : LongString.Replace(text, match => $"\"<{match.Length - 2:N0} characters left out>\"");

    /// <summary>Replaces the values of members that hold secrets (a password, a stream key) by asterisks.</summary>
    public static string Redact(string text) => Secret.Replace(text, "$1\"***\"");
}
