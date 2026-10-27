using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OBSStudioClient.Enums;
using OBSStudioClient.TestApp.Views;

namespace OBSStudioClient.TestApp;

/// <summary>The window of the app: a title bar, the navigation, and the page that is shown.</summary>
public sealed partial class MainWindow : Window
{
    /// <summary>Initializes a new instance of the <see cref="MainWindow"/> class.</summary>
    public MainWindow()
    {
        this.InitializeComponent();

        this.ExtendsContentIntoTitleBar = true;
        this.SetTitleBar(this.AppTitleBar);
        this.AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        this.AppWindow.Resize(new Windows.Graphics.SizeInt32(1360, 900));

        App.Session.StateChanged += (_, _) => this.ShowState();
        this.ShowState();

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
            "events" => typeof(EventsPage),
            "log" => typeof(LogPage),
            _ => null,
        };

        if (page is not null && this.NavFrame.CurrentSourcePageType != page)
        {
            this.NavFrame.Navigate(page);
        }
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
