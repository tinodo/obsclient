using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using OBSStudioClient.Enums;
using OBSStudioClient.TestApp.Catalog;

namespace OBSStudioClient.TestApp.Services;

/// <summary>How to connect to OBS Studio.</summary>
internal sealed record ConnectionSettings(string Host, int Port, string Password, bool AutoReconnect, EventSubscriptions Subscriptions, int RequestTimeout);

/// <summary>A scene or an input that OBS Studio reported, to pick from.</summary>
internal sealed record NamedReference(string Name, Guid? Uuid);

/// <summary>What happened when a request was sent.</summary>
internal sealed record InvocationResult(bool Succeeded, object? Value, TimeSpan Elapsed, Exception? Error, long StartSequence);

/// <summary>What the client counted of the session so far.</summary>
internal sealed record SessionStatistics(long BytesSent, long BytesReceived, int MessagesSent, int MessagesReceived);

/// <summary>
/// The connection of the app to OBS Studio: it owns the client, keeps the log of what goes over the wire, and sends the
/// requests that the pages ask for. Everything it raises is raised on the thread of the user interface.
/// </summary>
internal sealed class ObsSession
{
    private readonly DispatcherQueue _dispatcher;
    private ObsClient? _client;
    private volatile string? _lastClosed;

    /// <summary>Initializes a new instance of the <see cref="ObsSession"/> class; call it on the thread of the user interface.</summary>
    /// <param name="dispatcher">The dispatcher of the user interface.</param>
    public ObsSession(DispatcherQueue dispatcher)
    {
        this._dispatcher = dispatcher;
        this.Observations = new ObservationStore();
        this.Log = new WireLog(dispatcher, this.Observations);
        this.Settings = AppSettings.Load();
    }

    /// <summary>Occurs when the state of the connection, or what is known about OBS Studio, changed.</summary>
    public event EventHandler? StateChanged;

    /// <summary>Occurs when the lists of scenes and inputs were read again.</summary>
    public event EventHandler? ReferencesChanged;

    /// <summary>Occurs when the client raised an event of OBS Studio.</summary>
    public event EventHandler<SessionEvent>? EventReceived;

    /// <summary>Gets what the app remembers between runs.</summary>
    public AppSettings Settings { get; }

    /// <summary>Gets the shape of what OBS Studio sent.</summary>
    public ObservationStore Observations { get; }

    /// <summary>Gets the log of the client.</summary>
    public WireLog Log { get; }

    /// <summary>Gets the state of the connection.</summary>
    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

    /// <summary>Gets a value indicating whether requests can be sent.</summary>
    public bool IsConnected => this.State == ConnectionState.Connected;

    /// <summary>Gets the version of OBS Studio.</summary>
    public string? ObsVersion { get; private set; }

    /// <summary>Gets the version of obs-websocket.</summary>
    public string? ObsWebSocketVersion { get; private set; }

    /// <summary>Gets the platform OBS Studio runs on.</summary>
    public string? Platform { get; private set; }

    /// <summary>Gets why the last connection was closed, when OBS Studio said so.</summary>
    public string? LastClosed => this._lastClosed;

    /// <summary>Gets the scenes of OBS Studio.</summary>
    public IReadOnlyList<NamedReference> Scenes { get; private set; } = [];

    /// <summary>Gets the inputs of OBS Studio.</summary>
    public IReadOnlyList<NamedReference> Inputs { get; private set; } = [];

    /// <summary>Gets what the client counted of the session, or null when there is no client.</summary>
    public SessionStatistics? GetStatistics() => this._client is { } client
        ? new SessionStatistics(client.SessionBytesSent, client.SessionBytesReceived, client.SessionMessagesSent, client.SessionMessagesReceived)
        : null;

    /// <summary>Raises an event as if the client had raised it; for the smoke test, which shows the pages with something on them.</summary>
    internal void RaiseForSmokeTest(SessionEvent entry) => this.EventReceived?.Invoke(this, entry);

    /// <summary>Connects to OBS Studio and waits until the handshake is done.</summary>
    /// <param name="settings">How to connect.</param>
    /// <returns>Null when connected; otherwise what went wrong.</returns>
    public async Task<string?> ConnectAsync(ConnectionSettings settings)
    {
        this.DisposeClient();
        this._lastClosed = null;
        this.ObsVersion = null;
        this.ObsWebSocketVersion = null;
        this.Platform = null;

        var client = new ObsClient(this.Log) { RequestTimeout = settings.RequestTimeout };
        client.PropertyChanged += this.OnClientPropertyChanged;
        client.ConnectionClosed += this.OnConnectionClosed;
        RequestCatalog.Subscribe(client, this.OnClientEvent);
        this._client = client;

        try
        {
            var started = await client.ConnectAsync(settings.AutoReconnect, settings.Password, settings.Host, settings.Port, settings.Subscriptions);
            if (!started)
            {
                this.SetState(client.ConnectionState);
                return $"Could not connect to {settings.Host}:{settings.Port}. Is OBS Studio running, with its WebSocket server enabled?";
            }

            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (client.ConnectionState != ConnectionState.Connected)
            {
                if (client.ConnectionState == ConnectionState.Disconnected)
                {
                    await Task.Delay(300);
                    return this._lastClosed ?? "The connection was closed before it was established.";
                }

                if (DateTime.UtcNow > deadline)
                {
                    client.Disconnect();
                    return "OBS Studio did not complete the handshake within 15 seconds.";
                }

                await Task.Delay(50);
            }

            return null;
        }
        catch (Exception exception)
        {
            this.SetState(client.ConnectionState);
            return $"{exception.GetType().Name}: {exception.Message}";
        }
    }

    /// <summary>Closes the connection.</summary>
    public void Disconnect() => this._client?.Disconnect();

    /// <summary>Asks OBS Studio to send another set of events.</summary>
    /// <param name="subscriptions">The events to subscribe to.</param>
    public async Task ReidentifyAsync(EventSubscriptions subscriptions)
    {
        var client = this._client ?? throw new InvalidOperationException("Not connected.");
        await client.ReidentifyAsync(subscriptions);
    }

    /// <summary>Sends a request and waits for the response.</summary>
    /// <param name="request">The request.</param>
    /// <param name="arguments">The values of its parameters.</param>
    public async Task<InvocationResult> InvokeAsync(RequestDescriptor request, ArgumentSet arguments)
    {
        var start = this.Log.Sequence;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var client = this._client ?? throw new InvalidOperationException("Not connected to OBS Studio.");
            var invoke = request.Invoke ?? throw new InvalidOperationException($"{request.Name} cannot be sent on its own.");
            var value = await invoke(client, arguments);
            return new InvocationResult(true, value, stopwatch.Elapsed, null, start);
        }
        catch (Exception exception)
        {
            return new InvocationResult(false, null, stopwatch.Elapsed, exception, start);
        }
    }

    /// <summary>Reads the scenes and inputs of OBS Studio again, for the editors that offer them.</summary>
    public async Task RefreshReferencesAsync()
    {
        var client = this._client;
        if (client is null || !this.IsConnected)
        {
            return;
        }

        try
        {
            var scenes = await client.GetSceneList();
            var inputs = await client.GetInputList();
            this.Scenes = [.. scenes.Scenes.Select(scene => new NamedReference(scene.SceneName, scene.SceneUuid)).OrderBy(scene => scene.Name, StringComparer.CurrentCultureIgnoreCase)];
            this.Inputs = [.. inputs.Select(input => new NamedReference(input.InputName, input.InputUuid)).OrderBy(input => input.Name, StringComparer.CurrentCultureIgnoreCase)];
            this.ReferencesChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception)
        {
            this.Log.Note(LogLevel.Warning, $"The scenes and inputs could not be read: {exception.Message}");
        }
    }

    private async Task OnConnectedAsync(ObsClient client)
    {
        try
        {
            var version = await client.GetVersion();
            if (ReferenceEquals(client, this._client))
            {
                this.ObsVersion = version.ObsVersion;
                this.ObsWebSocketVersion = version.ObsWebSocketVersion;
                this.Platform = version.PlatformDescription;
                this.StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception exception)
        {
            this.Log.Note(LogLevel.Warning, $"The version of OBS Studio could not be read: {exception.Message}");
        }

        await this.RefreshReferencesAsync();
    }

    private void SetState(ConnectionState state)
    {
        if (this.State == state)
        {
            return;
        }

        var wasConnected = this.IsConnected;
        this.State = state;
        this.StateChanged?.Invoke(this, EventArgs.Empty);
        if (state == ConnectionState.Connected && !wasConnected && this._client is { } client)
        {
            _ = this.OnConnectedAsync(client);
        }
    }

    private void DisposeClient()
    {
        var client = this._client;
        this._client = null;
        if (client is null)
        {
            return;
        }

        client.PropertyChanged -= this.OnClientPropertyChanged;
        client.ConnectionClosed -= this.OnConnectionClosed;
        try
        {
            client.Disconnect();
            client.Dispose();
        }
        catch (Exception exception)
        {
            this.Log.Note(LogLevel.Debug, $"Disposing the previous client failed: {exception.Message}");
        }
    }

    private void OnClientPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ObsClient client)
        {
            return;
        }

        this._dispatcher.TryEnqueue(() =>
        {
            if (ReferenceEquals(client, this._client))
            {
                this.SetState(client.ConnectionState);
            }
        });
    }

    private void OnConnectionClosed(object? sender, ConnectionClosedEventArgs e)
    {
        this._lastClosed = $"{e.WebSocketCloseCode}: {e.WebSocketCloseDescription}";
        this.Log.Note(LogLevel.Information, $"The connection was closed: {this._lastClosed}");
    }

    private void OnClientEvent(string name, object? arguments)
    {
        string typed;
        try
        {
            typed = arguments is null ? "(the event has no data)" : JsonTools.Serialize(arguments);
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidOperationException)
        {
            typed = $"(the event arguments could not be shown: {exception.Message})";
        }

        var entry = new SessionEvent(DateTimeOffset.Now, name, RequestCatalog.CategoryOfEvent(name), typed, this.Log.LatestEvent(name)?.Text);
        this._dispatcher.TryEnqueue(() => this.EventReceived?.Invoke(this, entry));
    }
}
