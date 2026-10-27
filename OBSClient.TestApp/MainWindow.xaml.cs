using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OBSStudioClient.Enums;
using OBSStudioClient.TestApp.Views;
using Windows.Graphics;
using WinRT.Interop;

namespace OBSStudioClient.TestApp;

/// <summary>The window of the app: a title bar, the navigation, and the page that is shown.</summary>
public sealed partial class MainWindow : Window
{
    private const int PreferredWidth = 1360;
    private const int PreferredHeight = 900;

    /// <summary>Initializes a new instance of the <see cref="MainWindow"/> class.</summary>
    public MainWindow()
    {
        this.InitializeComponent();

        this.ExtendsContentIntoTitleBar = true;
        this.SetTitleBar(this.AppTitleBar);
        this.AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        this.SetInitialSize();

        App.Session.StateChanged += (_, _) => this.ShowState();
        App.Session.Batch.CollectionChanged += (_, _) => this.ShowBatchCount();
        this.ShowState();
        this.ShowBatchCount();

        // What the user chose to look at, and how to connect, is remembered; and the connection does not outlive the window.
        this.Closed += (_, _) =>
        {
            App.Session.Disconnect();
            App.Session.Settings.Save();
        };

        // A page whose XAML cannot be read must not close the app: the log says why, and so does the smoke test.
        this.NavFrame.NavigationFailed += (_, e) =>
        {
            e.Handled = true;
            var message = $"The page {e.SourcePageType.Name} could not be opened: {e.Exception}";
            SmokeTest.Record(message, isProblem: true);
            App.Session.Log.Note(Microsoft.Extensions.Logging.LogLevel.Error, message);
        };
    }

    /// <summary>Gets the page that is shown.</summary>
    internal object? CurrentPage => this.NavFrame.Content;

    /// <summary>Opens a page by the tag of its navigation item.</summary>
    internal void Navigate(string tag)
    {
        foreach (var item in this.NavView.MenuItems.OfType<NavigationViewItem>())
        {
            if ((string?)item.Tag == tag)
            {
                this.NavView.SelectedItem = item;
                return;
            }
        }
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    // The size is in pixels: scale it to the screen the window is on, and keep it inside the work area.
    private void SetInitialSize()
    {
        var scale = Math.Max(GetDpiForWindow(WindowNative.GetWindowHandle(this)), 96) / 96.0;
        var area = DisplayArea.GetFromWindowId(this.AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        var width = (int)Math.Min(PreferredWidth * scale, area.Width);
        var height = (int)Math.Min(PreferredHeight * scale, area.Height);
        this.AppWindow.Resize(new SizeInt32(width, height));
    }

    private void TitleBar_PaneToggleRequested(TitleBar sender, object args)
    {
        this.NavView.IsPaneOpen = !this.NavView.IsPaneOpen;
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item)
        {
            return;
        }

        var page = (string?)item.Tag switch
        {
            "connection" => typeof(ConnectionPage),
            "explorer" => typeof(ExplorerPage),
            "batch" => typeof(BatchPage),
            "events" => typeof(EventsPage),
            "log" => typeof(LogPage),
            _ => null,
        };

        if (page is not null && this.NavFrame.CurrentSourcePageType != page)
        {
            this.NavFrame.Navigate(page);
        }
    }

    private void ShowBatchCount()
    {
        var count = App.Session.Batch.Count;
        this.BatchBadge.Value = count;
        this.BatchBadge.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowState()
    {
        var session = App.Session;
        this.StatusText.Text = session.State switch
        {
            ConnectionState.Connected => $"Connected to OBS Studio {session.ObsVersion}".TrimEnd(),
            ConnectionState.Disconnected => "Not connected",
            var other => other.ToString() + "\u2026",
        };
    }
}
