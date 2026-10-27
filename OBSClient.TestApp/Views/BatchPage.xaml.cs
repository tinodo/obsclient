using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OBSStudioClient.Enums;
using OBSStudioClient.Messages;
using OBSStudioClient.TestApp.Analysis;
using OBSStudioClient.TestApp.Controls;
using OBSStudioClient.TestApp.Services;

namespace OBSStudioClient.TestApp.Views;

/// <summary>Sends several requests to OBS Studio as one batch, and shows how OBS Studio answered, in three ways.</summary>
public sealed partial class BatchPage : Page
{
    private readonly ObsSession _session = App.Session;
    private bool _busy;
    private string _typed = string.Empty;
    private string _raw = string.Empty;
    private string _comparison = string.Empty;

    /// <summary>Initializes a new instance of the <see cref="BatchPage"/> class.</summary>
    public BatchPage()
    {
        this.InitializeComponent();
        this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;

        this.ExecutionBox.ItemsSource = Enum.GetNames<RequestBatchExecutionType>().Where(name => name != nameof(RequestBatchExecutionType.None)).ToList();
        this.ExecutionBox.SelectedIndex = 0;
        this._session.Batch.CollectionChanged += (_, _) => this.Mirror();
        this._session.StateChanged += (_, _) => this.UpdateState();
        this.Mirror();
    }

    /// <summary>Gets the requests of the batch as the list shows them. The batch itself belongs to the session, which the Explorer adds to.</summary>
    public ObservableCollection<BatchItem> Items { get; } = [];

    /// <summary>Gets what the client returned for the last batch, as JSON; for the smoke test.</summary>
    internal string TypedResult => this._typed;

    /// <summary>Gets the message in which OBS Studio answered the last batch; for the smoke test.</summary>
    internal string RawResult => this._raw;

    /// <summary>Gets the comparison of the two; for the smoke test.</summary>
    internal string ComparisonResult => this._comparison;

    /// <summary>Gets what the page says about a batch that failed, or null; for the smoke test.</summary>
    internal string? Problem => this.ProblemBar.IsOpen ? $"{this.ProblemBar.Title}: {this.ProblemBar.Message}" : null;

    /// <summary>Gets what the page says about the last batch; for the smoke test.</summary>
    internal string Status => this.StatusLine.Text;

    /// <summary>Chooses how OBS Studio runs the batch, and whether it stops at the first failure; for the smoke test.</summary>
    internal void Configure(RequestBatchExecutionType executionType, bool haltOnFailure)
    {
        this.ExecutionBox.SelectedItem = executionType.ToString();
        this.HaltSwitch.IsOn = haltOnFailure;
    }

    /// <summary>Sends the batch, as the user would; for the smoke test.</summary>
    internal Task SendForSmokeTest() => this.SendAsync();

    /// <summary>Shows one of the three views of the result (0, 1 or 2); for the smoke test.</summary>
    internal void SelectResultTab(int index)
    {
        this.ResultTabs.SelectedItem = index switch
        {
            1 => this.RawTab,
            2 => this.ComparisonTab,
            _ => this.TypedTab,
        };
        this.ShowTab();
    }

    private static string DescribeResponses(IReadOnlyList<RequestResponseMessage> responses)
    {
        var rows = new List<Dictionary<string, object?>>();
        foreach (var response in responses)
        {
            var row = new Dictionary<string, object?>
            {
                ["requestType"] = response.RequestType.ToString(),
                ["success"] = response.RequestStatus.Result,
                ["code"] = (int)response.RequestStatus.Code,
                ["codeName"] = response.RequestStatus.Code.ToString(),
            };
            if (response.RequestStatus.Comment is { Length: > 0 } comment)
            {
                row["comment"] = comment;
            }

            if (response.RequestStatus.Result && response.ResponseData is { } data)
            {
                row["responseData"] = data;
            }

            rows.Add(row);
        }

        return JsonTools.Serialize(rows);
    }

    private static string CompareResponses(IReadOnlyList<RequestResponseMessage> responses)
    {
        var text = new StringBuilder();
        text.AppendLine("What OBS Studio sent (responseData) for each request, compared with what the client returned.");
        text.AppendLine(ShapeComparison.Legend);
        text.AppendLine();

        var number = 0;
        foreach (var response in responses)
        {
            number++;
            if (!response.RequestStatus.Result)
            {
                text.AppendLine($"{number}. {response.RequestType}: the request failed, so there is nothing to compare.");
            }
            else if (response.RawResponseData is not { ValueKind: JsonValueKind.Object } sent || response.ResponseData is null)
            {
                text.AppendLine($"{number}. {response.RequestType}: this request returns no data, so there is nothing to compare.");
            }
            else
            {
                using var returned = JsonTools.ToDocument(response.ResponseData)!;
                text.AppendLine($"{number}. {response.RequestType}:");
                text.Append(JsonShape.Compare(sent, returned.RootElement).Describe("   "));
            }

            text.AppendLine();
        }

        return text.ToString();
    }

    private string Render(string what, Func<string> produce)
    {
        try
        {
            return produce();
        }
        catch (Exception exception)
        {
            this._session.Log.Note(LogLevel.Warning, $"The {what} of the batch could not be shown: {exception.Message}");
            return $"(the {what} could not be shown: {exception.Message})";
        }
    }

    private void UpdateState()
    {
        var count = this.Items.Count;
        var index = this.ItemList.SelectedIndex;
        this.CountText.Text = count == 1 ? "1 request" : $"{count} requests";
        this.EmptyText.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
        this.MoveUpButton.IsEnabled = index > 0 && !this._busy;
        this.MoveDownButton.IsEnabled = index >= 0 && index < count - 1 && !this._busy;
        this.RemoveButton.IsEnabled = index >= 0 && !this._busy;
        this.ClearButton.IsEnabled = count > 0 && !this._busy;
        this.SendButton.IsEnabled = count > 0 && this._session.IsConnected && !this._busy;

        if (!this._busy && this.ResultCard.Visibility != Visibility.Visible)
        {
            this.StatusLine.Text = count > 0 && !this._session.IsConnected ? "Connect to OBS Studio first, on the Connection page." : string.Empty;
        }
    }

    private void SetBusy(bool busy)
    {
        this._busy = busy;
        this.Busy.IsActive = busy;
        this.UpdateState();
    }

    private void ShowTab()
    {
        var selected = this.ResultTabs.SelectedItem;
        var comparison = ReferenceEquals(selected, this.ComparisonTab);
        if (ReferenceEquals(selected, this.RawTab))
        {
            this.ResultBox.Text = this._raw;
            this.TabCaption.Text = "The message in which OBS Studio answered the batch, exactly as it was sent.";
        }
        else if (comparison)
        {
            this.ResultBox.Text = this._comparison;
            this.TabCaption.Text = "For each request that returns data: what OBS Studio sent, compared with what the client returned.";
        }
        else
        {
            this.ResultBox.Text = this._typed;
            this.TabCaption.Text = "The responses as the client returns them, one for each request that OBS Studio ran.";
        }

        this.ResultBox.TextWrapping = comparison ? TextWrapping.Wrap : TextWrapping.NoWrap;
    }

    private void Mirror()
    {
        var selected = this.ItemList.SelectedItem as BatchItem;
        this.Items.Clear();
        foreach (var item in this._session.Batch)
        {
            this.Items.Add(item);
        }

        if (selected is not null && this.Items.Contains(selected))
        {
            this.ItemList.SelectedItem = selected;
        }

        this.UpdateState();
    }

    private void MoveUpButton_Click(object sender, RoutedEventArgs e)
    {
        var index = this.ItemList.SelectedIndex;
        if (index > 0)
        {
            this._session.Batch.Move(index, index - 1);
        }
    }

    private void MoveDownButton_Click(object sender, RoutedEventArgs e)
    {
        var index = this.ItemList.SelectedIndex;
        if (index >= 0 && index < this.Items.Count - 1)
        {
            this._session.Batch.Move(index, index + 1);
        }
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        var index = this.ItemList.SelectedIndex;
        if (index >= 0)
        {
            this._session.Batch.RemoveAt(index);
            this.ItemList.SelectedIndex = Math.Min(index, this.Items.Count - 1);
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e) => this._session.Batch.Clear();

    private void ItemList_SelectionChanged(object sender, SelectionChangedEventArgs e) => this.UpdateState();

    private void ExecutionBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => this.ExecutionNote.Text = (this.ExecutionBox.SelectedItem as string) switch
    {
        nameof(RequestBatchExecutionType.SerialRealtime) => "Processes all requests serially, as fast as possible. To introduce a delay, add a Sleep request with sleepMillis.",
        nameof(RequestBatchExecutionType.SerialFrame) => "Processes all requests serially, in sync with the graphics thread; designed to provide high accuracy for animations. To introduce a delay, add a Sleep request with sleepFrames.",
        nameof(RequestBatchExecutionType.Parallel) => "Processes all requests using all the available threads of the thread pool. Mainly experimental: it shows its colors for requests that need a lot of processing, such as GetSourceScreenshot. A Sleep request is not available.",
        _ => string.Empty,
    };

    private async void SendButton_Click(object sender, RoutedEventArgs e) => await this.SendAsync();

    private async Task SendAsync()
    {
        if (this.Items.Count == 0 || this.ExecutionBox.SelectedItem is not string executionName)
        {
            return;
        }

        int? timeout = null;
        var timeoutText = this.TimeoutBox.Text.Trim();
        if (timeoutText.Length > 0)
        {
            if (!int.TryParse(timeoutText, NumberStyles.None, CultureInfo.InvariantCulture, out var milliseconds) || milliseconds < 1)
            {
                Fluent.ShowWarning(this.ProblemBar, "Check the timeout", "Enter the timeout as a whole number of milliseconds, such as 5000, or leave it empty.");
                return;
            }

            timeout = milliseconds;
        }

        var requested = this.Items.Count;
        this.ProblemBar.IsOpen = false;
        this.SetBusy(true);
        var result = await this._session.SendBatchAsync(Enum.Parse<RequestBatchExecutionType>(executionName), this.HaltSwitch.IsOn, timeout);
        this.SetBusy(false);

        var frame = result.RequestId is null ? null : this._session.Log.FindBatchResponse(result.RequestId, result.StartSequence);
        var responses = result.Responses ?? [];
        this._typed = this.Render("responses", () => result.Succeeded ? DescribeResponses(responses) : "(the batch failed)");
        this._raw = frame is null ? "(the response of OBS Studio was not found in the log)" : JsonTools.Pretty(frame.Text);
        this._comparison = this.Render("comparison", () => result.Succeeded ? CompareResponses(responses) : "Nothing to compare: the batch failed.");

        if (result.Error is { } error)
        {
            Fluent.ShowFailure(this.ProblemBar, error);
            this.StatusLine.Text = $"The batch of {requested} {(requested == 1 ? "request" : "requests")} failed after {result.Elapsed.TotalMilliseconds:N0} ms.";
        }
        else
        {
            var failed = responses.Count(response => !response.RequestStatus.Result);
            this.StatusLine.Text = $"OBS Studio ran {responses.Length} of {requested} {(requested == 1 ? "request" : "requests")}, {failed} failed, in {result.Elapsed.TotalMilliseconds:N0} ms.";
        }

        this.ResultCard.Visibility = Visibility.Visible;
        this.ResultTabs.SelectedItem = this.TypedTab;
        this.ShowTab();
        this.ResultCard.StartBringIntoView();
    }

    private void ResultTabs_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args) => this.ShowTab();

    private async void CopyButton_Click(object sender, RoutedEventArgs e) => await ClipboardHelper.CopyAsync(this.CopyButton, this.ResultBox.Text);
}
