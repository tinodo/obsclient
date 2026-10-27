using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace OBSStudioClient.TestApp.Services;

/// <summary>Writes what the app saw to files that can be analysed: the log as JSON lines, and the shapes of what OBS Studio sent.</summary>
internal static class SessionExporter
{
    /// <summary>Writes the log as JSON lines, one entry per line.</summary>
    /// <param name="entries">The entries.</param>
    /// <param name="redact">Whether to replace passwords and keys by asterisks.</param>
    /// <returns>The path of the file.</returns>
    public static string ExportLog(IEnumerable<LogEntry> entries, bool redact)
    {
        var path = NewPath("obs-session", "jsonl");
        using var file = new StreamWriter(path, false, new UTF8Encoding(false));
        foreach (var entry in entries)
        {
            file.WriteLine(ToLine(entry, redact));
        }

        return path;
    }

    /// <summary>Writes the shapes of everything OBS Studio sent.</summary>
    /// <returns>The path of the file.</returns>
    public static string ExportShapes(ObservationStore observations, string? obsVersion, string? obsWebSocketVersion)
    {
        var path = NewPath("obs-observed-shapes", "json");
        File.WriteAllText(path, observations.ToJson(obsVersion, obsWebSocketVersion), new UTF8Encoding(false));
        return path;
    }

    /// <summary>Opens the folder of a file, with the file selected.</summary>
    public static void ShowInFolder(string? path)
    {
        if (path is not null && File.Exists(path))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
        }
    }

    private static string NewPath(string name, string extension)
    {
        Directory.CreateDirectory(AppSettings.ExportFolder);
        return Path.Combine(AppSettings.ExportFolder, $"{name}-{DateTime.Now:yyyyMMdd-HHmmss}.{extension}");
    }

    private static string ToLine(LogEntry entry, bool redact)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("time", entry.Time);
            writer.WriteString("kind", entry.Kind.ToString().ToLowerInvariant());
            writer.WriteString("level", entry.Level.ToString());
            if (entry.OpCode is { } op)
            {
                writer.WriteNumber("op", op);
            }

            if (entry.RequestType is not null)
            {
                writer.WriteString("requestType", entry.RequestType);
            }

            if (entry.EventType is not null)
            {
                writer.WriteString("eventType", entry.EventType);
            }

            writer.WriteString("title", entry.Title);
            var text = redact ? JsonTools.Redact(entry.Text) : entry.Text;
            if (entry.Kind != LogKind.Client && IsJson(text))
            {
                writer.WritePropertyName("frame");
                writer.WriteRawValue(text);
            }
            else
            {
                writer.WriteString("text", text);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static bool IsJson(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
