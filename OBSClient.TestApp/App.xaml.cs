using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Extensions.Logging;
using OBSStudioClient.TestApp.Services;

namespace OBSStudioClient.TestApp;

/// <summary>The test app of OBSClient.</summary>
public partial class App : Application
{
    private Window? _window;

    /// <summary>Initializes a new instance of the <see cref="App"/> class.</summary>
    public App()
    {
        this.InitializeComponent();
        this.UnhandledException += this.OnUnhandledException;
    }

    /// <summary>Gets the connection to OBS Studio, which the pages share.</summary>
    internal static ObsSession Session { get; private set; } = null!;

    /// <summary>Gets the window of the app.</summary>
    internal static MainWindow? MainWindow { get; private set; }

    /// <inheritdoc/>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        Session = new ObsSession(DispatcherQueue.GetForCurrentThread());
        MainWindow = new MainWindow();
        this._window = MainWindow;
        this._window.Activate();

        if (SmokeTest.IsRequested(out var path))
        {
            AppSettings.Persist = false;
            _ = SmokeTest.RunAsync(MainWindow, path);
        }
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        // A tool for trying things out should not close because one of them went wrong: say so in the log instead.
        e.Handled = true;
        SmokeTest.Record($"Unhandled exception: {e.Exception}", isProblem: true);
        Session?.Log.Note(LogLevel.Error, $"Unhandled exception: {e.Exception}");
    }
}
