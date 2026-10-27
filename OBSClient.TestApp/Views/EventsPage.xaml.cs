using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OBSStudioClient.TestApp.Controls;
using OBSStudioClient.TestApp.Services;

namespace OBSStudioClient.TestApp.Views;

/// <summary>Shows the events that OBS Studio sends, as the client raises them.</summary>
public sealed partial class EventsPage : Page
{
    private const int MaxShown = 3000;

    private readonly ObsSession _session = App.Session;

    /// <summary>Initializes a new instance of the <see cref="EventsPage"/> class.</summary>
    public EventsPage()
    {
        this.InitializeComponent();
        this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        this._session.EventReceived += this.OnEventReceived;

        // The page is made when it is first opened, which may be after the events were raised: the session has kept them.
        this.Rebuild();
    }

    /// <summary>Gets the events that match the filter.</summary>
    public ObservableCollection<SessionEvent> Visible { get; } = [];

    /// <summary>Gets the events that are shown; for the smoke test.</summary>
    internal IReadOnlyList<SessionEvent> Shown => this.Visible;

    /// <summary>Selects the first event, for the smoke test.</summary>
    internal void SelectFirst()
    {
        if (this.Visible.Count > 0)
        {
            this.EventList.SelectedIndex = 0;
        }
    }

    private bool Matches(SessionEvent entry)
    {
        var filter = this.FilterBox.Text.Trim();
        return filter.Length == 0
            || entry.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || entry.Category.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || entry.TypedJson.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private void OnEventReceived(object? sender, SessionEvent entry)
    {
        if (this.PauseButton.IsChecked == true)
        {
            // The session keeps the event: it is shown when the page is resumed.
            this.ShowCount();
            return;
        }

        if (this.Matches(entry))
        {
            this.Visible.Add(entry);
            if (this.Visible.Count > MaxShown)
            {
                this.Visible.RemoveAt(0);
            }

            if (this.FollowButton.IsChecked == true && this.EventList.SelectedItem is null)
            {
                this.EventList.ScrollIntoView(entry);
            }
        }

        this.ShowCount();
    }

    private void ShowCount()
    {
        this.CountText.Text = $"{this.Visible.Count:N0} shown, {this._session.EventsReceived:N0} received" + (this.PauseButton.IsChecked == true ? " \u00b7 paused" : string.Empty);
        this.EmptyText.Visibility = this.Visible.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Rebuild()
    {
        this.Visible.Clear();
        foreach (var entry in this._session.EventHistory.Where(this.Matches).TakeLast(MaxShown))
        {
            this.Visible.Add(entry);
        }

        this.ShowCount();
    }

    private void ShowDetail()
    {
        if (this.EventList.SelectedItem is not SessionEvent entry)
        {
            this.DetailTitle.Text = "Select an event";
            this.DetailBox.Text = string.Empty;
            return;
        }

        this.DetailTitle.Text = $"{entry.Name} \u00b7 {entry.Category} \u00b7 {entry.TimeText}";
        this.DetailBox.Text = ReferenceEquals(this.DetailTabs.SelectedItem, this.RawTab)
            ? entry.RawJson is null ? "(OBS Studio's message was not found in the log)" : JsonTools.Pretty(entry.RawJson)
            : entry.TypedJson;
    }

    private void FilterBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            this.Rebuild();
        }
    }

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

    /// <summary>Forgets the events that were received, here and in the session.</summary>
    internal void Reset()
    {
        this._session.ClearEventHistory();
        this.Visible.Clear();
        this.ShowDetail();
        this.ShowCount();
    }

    private void EventList_SelectionChanged(object sender, SelectionChangedEventArgs e) => this.ShowDetail();

    private void DetailTabs_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args) => this.ShowDetail();

    private async void CopyButton_Click(object sender, RoutedEventArgs e) => await ClipboardHelper.CopyAsync(this.CopyButton, this.DetailBox.Text);
}
