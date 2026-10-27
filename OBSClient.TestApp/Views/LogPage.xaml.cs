using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OBSStudioClient.TestApp.Controls;
using OBSStudioClient.TestApp.Services;

namespace OBSStudioClient.TestApp.Views;

/// <summary>Chooses how an entry of the log looks: entries that deserve attention are marked.</summary>
public sealed class LogTemplateSelector : DataTemplateSelector
{
    /// <summary>Gets or sets the template of an ordinary entry.</summary>
    public DataTemplate? Normal { get; set; }

    /// <summary>Gets or sets the template of an entry that deserves attention.</summary>
    public DataTemplate? Problem { get; set; }

    /// <inheritdoc/>
    protected override DataTemplate SelectTemplateCore(object item) =>
        (item is LogEntry { IsProblem: true } ? this.Problem : this.Normal) ?? throw new InvalidOperationException("The templates of the log are missing.");

    /// <inheritdoc/>
    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container) => this.SelectTemplateCore(item);
}

/// <summary>Shows everything that went over the wire and everything the client reported, and exports it for analysis.</summary>
public sealed partial class LogPage : Page
{
    private const int MaxShown = 2500;

    private readonly ObsSession _session = App.Session;
    private string? _lastExport;

    /// <summary>Initializes a new instance of the <see cref="LogPage"/> class.</summary>
    public LogPage()
    {
        this.InitializeComponent();
        this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        this._session.Log.Added += this.OnAdded;
        this.Rebuild();
    }

    /// <summary>Gets the entries that match the filters.</summary>
    public ObservableCollection<LogEntry> Visible { get; } = [];

    /// <summary>Gets the entries that are shown; for the smoke test.</summary>
    internal IReadOnlyList<LogEntry> Shown => this.Visible;

    /// <summary>Selects the first entry that is shown, for the smoke test.</summary>
    internal void SelectFirst()
    {
        if (this.Visible.Count > 0)
        {
            this.EntryList.SelectedIndex = 0;
        }
    }

    private bool Matches(LogEntry entry)
    {
        var shown = entry.Kind switch
        {
            LogKind.Sent => this.SentButton.IsChecked == true,
            LogKind.Received => this.ReceivedButton.IsChecked == true,
            _ => this.ClientButton.IsChecked == true,
        };
        if (!shown)
        {
            return false;
        }

        var filter = this.FilterBox.Text.Trim();
        return filter.Length == 0
            || entry.Title.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || entry.Text.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private void OnAdded(object? sender, IReadOnlyList<LogEntry> entries)
    {
        if (this.PauseButton.IsChecked == true)
        {
            return;
        }

        foreach (var entry in entries.Where(this.Matches))
        {
            this.Visible.Add(entry);
        }

        while (this.Visible.Count > MaxShown)
        {
            this.Visible.RemoveAt(0);
        }

        if (this.FollowButton.IsChecked == true && this.EntryList.SelectedItem is null && this.Visible.Count > 0)
        {
            this.EntryList.ScrollIntoView(this.Visible[this.Visible.Count - 1]);
        }

        this.ShowCount();
    }

    private void Rebuild()
    {
        this.Visible.Clear();
        foreach (var entry in this._session.Log.Snapshot().Where(this.Matches).TakeLast(MaxShown))
        {
            this.Visible.Add(entry);
        }

        this.ShowCount();
    }

    private void ShowCount()
    {
        this.CountText.Text = $"{this.Visible.Count:N0} shown, {this._session.Log.Sequence:N0} in total" + (this.PauseButton.IsChecked == true ? " \u00b7 paused" : string.Empty);
        this.EmptyText.Visibility = this.Visible.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowDetail()
    {
        if (this.EntryList.SelectedItem is not LogEntry entry)
        {
            this.DetailTitle.Text = "Select an entry";
            this.DetailBox.Text = string.Empty;
            return;
        }

        this.DetailTitle.Text = $"{entry.Title} \u00b7 {entry.TimeText}";
        this.DetailBox.Text = entry.Kind == LogKind.Client ? entry.Text : JsonTools.Pretty(entry.Text);
    }

    private void ShowExport(string title, string path)
    {
        this._lastExport = path;
        this.ExportBar.Severity = InfoBarSeverity.Success;
        this.ExportBar.Title = title;
        this.ExportBar.Message = path;
        this.ExportBar.IsOpen = true;
    }

    private void ShowExportError(Exception exception)
    {
        this._lastExport = null;
        this.ExportBar.Severity = InfoBarSeverity.Error;
        this.ExportBar.Title = "The export failed";
        this.ExportBar.Message = exception.Message;
        this.ExportBar.IsOpen = true;
    }

    private void FilterBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            this.Rebuild();
        }
    }

    private void Filter_Click(object sender, RoutedEventArgs e) => this.Rebuild();

    private void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        if (this.PauseButton.IsChecked != true)
        {
            this.Rebuild();
        }
        else
        {
            this.ShowCount();
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e) => this.Reset();

    /// <summary>Forgets the log, here and in the session.</summary>
    internal void Reset()
    {
        this._session.Log.Clear();
        this.Visible.Clear();
        this.ShowDetail();
        this.ShowCount();
    }

    private void EntryList_SelectionChanged(object sender, SelectionChangedEventArgs e) => this.ShowDetail();

    private void ExportLogButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            this.ShowExport("The log was written", SessionExporter.ExportLog(this._session.Log.Snapshot(), this.RedactBox.IsChecked == true));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            this.ShowExportError(exception);
        }
    }

    private void ExportShapesButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            this.ShowExport(
                $"The shapes of {this._session.Observations.Count} responses and events were written",
                SessionExporter.ExportShapes(this._session.Observations, this._session.ObsVersion, this._session.ObsWebSocketVersion));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            this.ShowExportError(exception);
        }
    }

    private void ShowInFolderButton_Click(object sender, RoutedEventArgs e) => SessionExporter.ShowInFolder(this._lastExport);

    private async void CopyButton_Click(object sender, RoutedEventArgs e) => await ClipboardHelper.CopyAsync(this.CopyButton, this.DetailBox.Text);
}
