using System.Text.Json;
using OBSStudioClient.Enums;

namespace OBSStudioClient.TestApp.Services;

/// <summary>What the app remembers between runs: how to connect, never the password.</summary>
internal sealed class AppSettings
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>Gets or sets the host that runs OBS Studio.</summary>
    public string Host { get; set; } = "localhost";

    /// <summary>Gets or sets the port of the WebSocket server of OBS Studio.</summary>
    public int Port { get; set; } = 4455;

    /// <summary>Gets or sets a value indicating whether the client reconnects by itself.</summary>
    public bool AutoReconnect { get; set; }

    /// <summary>Gets or sets the event subscriptions.</summary>
    public int Subscriptions { get; set; } = (int)EventSubscriptions.All;

    /// <summary>Gets or sets the time, in milliseconds, that a request may take.</summary>
    public int RequestTimeout { get; set; } = 5000;

    /// <summary>Gets or sets the request that was selected last.</summary>
    public string? LastRequest { get; set; }

    /// <summary>Gets or sets a value indicating whether <see cref="Save"/> writes the file; the smoke test must not touch the settings of the user.</summary>
    public static bool Persist { get; set; } = true;

    /// <summary>Gets the folder where the app keeps its settings.</summary>
    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OBSClient.TestApp");

    /// <summary>Gets the folder where the app writes its exports, where the user can find them.</summary>
    public static string ExportFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "OBSClient Test App");

    private static string FilePath => Path.Combine(Folder, "settings.json");

    /// <summary>Reads the settings; a missing or damaged file gives the defaults.</summary>
    public static AppSettings Load()
    {
        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AppSettings();
        }
    }

    /// <summary>Writes the settings; a failure is not worth bothering the user with.</summary>
    public void Save()
    {
        if (!Persist)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Options));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The settings are a convenience.
        }
    }
}
