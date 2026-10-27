using System.Globalization;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OBSStudioClient.Enums;
using OBSStudioClient.TestApp.Services;

namespace OBSStudioClient.TestApp.Views;

/// <summary>Where to connect, which events to receive, and what is known about the session.</summary>
public sealed partial class ConnectionPage : Page
{
    private static readonly (string Name, EventSubscriptions Flag)[] Events =
    [
        ("General", EventSubscriptions.General),
        ("Config", EventSubscriptions.Config),
        ("Scenes", EventSubscriptions.Scenes),
        ("Inputs", EventSubscriptions.Inputs),
        ("Transitions", EventSubscriptions.Transitions),
        ("Filters", EventSubscriptions.Filters),
        ("Outputs", EventSubscriptions.Outputs),
        ("Scene items", EventSubscriptions.SceneItems),
        ("Media inputs", EventSubscriptions.MediaInputs),
        ("Vendors", EventSubscriptions.Vendors),
        ("UI", EventSubscriptions.Ui),
        ("Canvases", EventSubscriptions.Canvases),
    ];

    private static readonly (string Name, EventSubscriptions Flag)[] HighVolumeEvents =
    [
        ("Input volume meters", EventSubscriptions.InputVolumeMeters),
        ("Input active state changed", EventSubscriptions.InputActiveStateChanged),
        ("Input show state changed", EventSubscriptions.InputShowStateChanged),
        ("Scene item transform changed", EventSubscriptions.SceneItemTransformChanged),
    ];

    private readonly ObsSession _session = App.Session;
    private readonly Dictionary<EventSubscriptions, CheckBox> _boxes = [];
    private readonly DispatcherQueueTimer _timer;
    private string? _error;
    private bool _connecting;

    /// <summary>Initializes a new instance of the <see cref="ConnectionPage"/> class.</summary>
    public ConnectionPage()
    {
        this.InitializeComponent();
        this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;

        var settings = this._session.Settings;
        this.HostBox.Text = settings.Host;
        this.PortBox.Text = settings.Port.ToString(CultureInfo.InvariantCulture);
        this.TimeoutBox.Text = settings.RequestTimeout.ToString(CultureInfo.InvariantCulture);
        this.AutoReconnectSwitch.IsOn = settings.AutoReconnect;

        var chosen = (EventSubscriptions)settings.Subscriptions;
        this.AddBoxes(this.SubscriptionGrid, Events, chosen);
        this.AddBoxes(this.HighVolumeGrid, HighVolumeEvents, chosen);

        this._session.StateChanged += (_, _) => this.ShowState();
        this._timer = this.DispatcherQueue.CreateTimer();
        this._timer.Interval = TimeSpan.FromSeconds(1);
        this._timer.IsRepeating = true;
        this._timer.Tick += (_, _) => this.ShowStatistics();
        this.Loaded += (_, _) =>
        {
            this._timer.Start();
            this.ShowState();
        };
        this.Unloaded += (_, _) => this._timer.Stop();
        this.ShowState();
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:N1} KB",
        _ => $"{bytes / (1024.0 * 1024.0):N1} MB",
    };

    private void AddBoxes(VariableSizedWrapGrid grid, (string Name, EventSubscriptions Flag)[] flags, EventSubscriptions chosen)
    {
        foreach (var (name, flag) in flags)
        {
            var box = new CheckBox { Content = name, IsChecked = (chosen & flag) == flag };
            this._boxes[flag] = box;
            grid.Children.Add(box);
        }
    }

    private EventSubscriptions ReadSubscriptions()
    {
        var result = EventSubscriptions.None;
        foreach (var (flag, box) in this._boxes)
        {
            if (box.IsChecked == true)
            {
                result |= flag;
            }
        }

        return result;
    }

    private bool TryReadSettings(out ConnectionSettings settings, out string problem)
    {
        settings = new ConnectionSettings(string.Empty, 0, string.Empty, false, EventSubscriptions.None, 0);
        var host = this.HostBox.Text.Trim();
        if (host.Length == 0)
        {
            problem = "Enter the host that runs OBS Studio, such as localhost.";
            return false;
        }

        if (!int.TryParse(this.PortBox.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var port) || port is < 1 or > 65534)
        {
            problem = "The port is a number between 1 and 65534; OBS Studio uses 4455 by default.";
            return false;
        }

        if (!int.TryParse(this.TimeoutBox.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var timeout) || timeout < 150)
        {
            problem = "The request timeout is a number of milliseconds, at least 150.";
            return false;
        }

        settings = new ConnectionSettings(host, port, this.PasswordInput.Password, this.AutoReconnectSwitch.IsOn, this.ReadSubscriptions(), timeout);
        problem = string.Empty;
        return true;
    }

    private void ShowState()
    {
        var session = this._session;
        var bar = this.StatusBar;
        switch (session.State)
        {
            case ConnectionState.Connected:
                bar.Severity = InfoBarSeverity.Success;
                bar.Title = $"Connected to OBS Studio {session.ObsVersion}".TrimEnd();
                bar.Message = session.ObsWebSocketVersion is null ? string.Empty : $"obs-websocket {session.ObsWebSocketVersion} on {session.Platform}.";
                break;
            case ConnectionState.Disconnected when this._error is not null:
                bar.Severity = InfoBarSeverity.Error;
                bar.Title = "Could not connect";
                bar.Message = this._error;
                break;
            case ConnectionState.Disconnected:
                bar.Severity = InfoBarSeverity.Informational;
                bar.Title = "Not connected";
                bar.Message = session.LastClosed is null ? "Enter how to reach OBS Studio, and connect." : $"The last connection was closed: {session.LastClosed}";
                break;
            default:
                bar.Severity = InfoBarSeverity.Informational;
                bar.Title = session.State.ToString() + "\u2026";
                bar.Message = string.Empty;
                break;
        }

        this.ConnectButton.IsEnabled = session.State == ConnectionState.Disconnected && !this._connecting;
        this.DisconnectButton.IsEnabled = session.State != ConnectionState.Disconnected;
        this.ApplyButton.IsEnabled = session.IsConnected;
        this.VersionText.Text = session.IsConnected
            ? $"OBS Studio {session.ObsVersion}, obs-websocket {session.ObsWebSocketVersion}, {session.Platform}. {session.Scenes.Count} scenes and {session.Inputs.Count} inputs."
            : "Not connected.";
        this.ShowStatistics();
    }

    private void ShowStatistics()
    {
        var statistics = this._session.GetStatistics();
        this.StatisticsText.Text = statistics is null || !this._session.IsConnected
            ? string.Empty
            : $"Sent {statistics.MessagesSent:N0} messages ({FormatBytes(statistics.BytesSent)}); received {statistics.MessagesReceived:N0} ({FormatBytes(statistics.BytesReceived)}).";
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        if (!this.TryReadSettings(out var settings, out var problem))
        {
            this._error = problem;
            this.ShowState();
            return;
        }

        var saved = this._session.Settings;
        saved.Host = settings.Host;
        saved.Port = settings.Port;
        saved.RequestTimeout = settings.RequestTimeout;
        saved.AutoReconnect = settings.AutoReconnect;
        saved.Subscriptions = (int)settings.Subscriptions;
        saved.Save();

        this._error = null;
        this._connecting = true;
        this.Busy.IsActive = true;
        this.ShowState();

        var error = await this._session.ConnectAsync(settings);

        this._connecting = false;
        this.Busy.IsActive = false;
        this._error = error;
        this.ShowState();
    }

    private void DisconnectButton_Click(object sender, RoutedEventArgs e)
    {
        this._error = null;
        this._session.Disconnect();
    }

    private async void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        var subscriptions = this.ReadSubscriptions();
        try
        {
            await this._session.ReidentifyAsync(subscriptions);
            this._session.Settings.Subscriptions = (int)subscriptions;
            this._session.Settings.Save();
            this.NoticeBar.Severity = InfoBarSeverity.Success;
            this.NoticeBar.Title = "Event subscriptions updated";
            this.NoticeBar.Message = subscriptions == EventSubscriptions.None ? "No events will be sent." : subscriptions.ToString();
        }
        catch (Exception exception)
        {
            this.NoticeBar.Severity = InfoBarSeverity.Error;
            this.NoticeBar.Title = "The event subscriptions could not be updated";
            this.NoticeBar.Message = exception.Message;
        }

        this.NoticeBar.IsOpen = true;
    }
}
