using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OBSStudioClient.Messages;
using OBSStudioClient.TestApp.Analysis;
using OBSStudioClient.TestApp.Catalog;
using OBSStudioClient.TestApp.Controls;
using OBSStudioClient.TestApp.Services;

namespace OBSStudioClient.TestApp.Views;

/// <summary>Sends any request of the protocol to OBS Studio and shows what came back, in three ways.</summary>
public sealed partial class ExplorerPage : Page
{
    private readonly ObsSession _session = App.Session;
    private readonly ParameterForm _form;
    private RequestDescriptor? _current;
    private bool _busy;
    private string _typed = string.Empty;
    private string _raw = string.Empty;
    private string _comparison = string.Empty;

    /// <summary>Initializes a new instance of the <see cref="ExplorerPage"/> class.</summary>
    public ExplorerPage()
    {
        this.InitializeComponent();
        this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;

        this._form = new ParameterForm(this._session);
        this.FormHost.Children.Add(this._form);
        this.BuildTree(string.Empty);
        this._session.StateChanged += (_, _) => this.UpdateButtons();

        if (RequestCatalog.Find(this._session.Settings.LastRequest) is { } last)
        {
            this.Show(last);
        }
    }

    /// <summary>Shows a request, as when the user chooses it; for the smoke test.</summary>
    internal void OpenRequest(RequestDescriptor request) => this.Show(request);

    /// <summary>Shows a request, fills in every parameter and reads the values back; for the smoke test.</summary>
    internal ArgumentSet FillAndCollect(RequestDescriptor request)
    {
        this.Show(request);
        this._form.FillForSmokeTest();
        return this._form.Collect();
    }

    private static string Compare(InvocationResult result, LogEntry? frame)
    {
        if (!result.Succeeded)
        {
            return "Nothing to compare: the request did not succeed.";
        }

        if (result.Value is null)
        {
            return "This request returns no data, so there is nothing to compare.";
        }

        if (frame is null)
        {
            return "What OBS Studio sent was not found in the log (is the log full?), so it cannot be compared.";
        }

        try
        {
            using var sent = JsonDocument.Parse(frame.Text);
            if (!sent.RootElement.TryGetProperty("d", out var data) || !data.TryGetProperty("responseData", out var responseData))
            {
                return "OBS Studio sent no response data.";
            }

            using var returned = JsonTools.ToDocument(result.Value)!;
            var comparison = JsonShape.Compare(responseData, returned.RootElement);

            return "What OBS Studio sent (responseData), compared with what the client returned." + Environment.NewLine
                + ShapeComparison.Legend + Environment.NewLine
                + Environment.NewLine
                + comparison.Describe();
        }
        catch (JsonException exception)
        {
            return $"The messages could not be compared: {exception.Message}";
        }
    }

    private void BuildTree(string filter)
    {
        var text = filter.Trim();
        this.RequestTree.RootNodes.Clear();
        foreach (var category in RequestCatalog.Categories)
        {
            var requests = RequestCatalog.All
                .Where(request => request.Category == category)
                .Where(request => text.Length == 0
                    || request.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
                    || request.Description.Contains(text, StringComparison.OrdinalIgnoreCase))
                .OrderBy(request => request.Name, StringComparer.Ordinal)
                .ToList();
            if (requests.Count == 0)
            {
                continue;
            }

            var node = new TreeViewNode { Content = $"{category} ({requests.Count})", IsExpanded = text.Length > 0 };
            foreach (var request in requests)
            {
                node.Children.Add(new TreeViewNode { Content = request });
            }

            this.RequestTree.RootNodes.Add(node);
        }

        this.NoMatchText.Visibility = this.RequestTree.RootNodes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Show(RequestDescriptor request)
    {
        this._current = request;
        this._session.Settings.LastRequest = request.Name;

        this.WelcomePanel.Visibility = Visibility.Collapsed;
        this.DetailPanel.Visibility = Visibility.Visible;
        this.RequestTitle.Text = request.Name;
        this.RequestMeta.Text = $"{request.Category} \u00b7 obs-websocket {request.Since}" + (request.Invoke is null ? " \u00b7 only in a batch" : string.Empty);
        this.RequestDescription.Text = request.Description;
        this._form.Load(request.Parameters);
        this.NoParameters.Visibility = request.Parameters.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        this.ProblemBar.IsOpen = false;
        this.ResultCard.Visibility = Visibility.Collapsed;
        this.StatusLine.Text = string.Empty;
        ToolTipService.SetToolTip(
            this.BatchButton,
            request.AddToBatch is null
                ? "Only the requests of the protocol can be put in a batch; this is a shortcut of the client."
                : "Puts the request in the batch, to send it with others from the Batch page.");
        this.UpdateButtons();
    }

    private void UpdateButtons()
    {
        var request = this._current;
        this.InvokeButton.IsEnabled = request?.Invoke is not null && this._session.IsConnected && !this._busy;
        this.BatchButton.IsEnabled = request?.AddToBatch is not null && !this._busy;
        this.RefreshButton.IsEnabled = this._session.IsConnected && !this._busy;

        if (request is null || this._busy || this.ResultCard.Visibility == Visibility.Visible)
        {
            return;
        }

        this.StatusLine.Text = request.Invoke is null
            ? "OBS Studio only accepts this request in a batch."
            : this._session.IsConnected ? string.Empty : "Connect to OBS Studio first, on the Connection page.";
    }

    private void SetBusy(bool busy)
    {
        this._busy = busy;
        this.Busy.IsActive = busy;
        this.UpdateButtons();
    }

    private void ShowTab()
    {
        var selected = this.ResultTabs.SelectedItem;
        var comparison = ReferenceEquals(selected, this.ComparisonTab);
        if (ReferenceEquals(selected, this.RawTab))
        {
            this.ResultBox.Text = this._raw;
            this.TabCaption.Text = "The response exactly as OBS Studio sent it.";
        }
        else if (comparison)
        {
            this.ResultBox.Text = this._comparison;
            this.TabCaption.Text = "What OBS Studio sent, compared with what the client returned.";
        }
        else
        {
            this.ResultBox.Text = this._typed;
            this.TabCaption.Text = "What the method of the client returned, as JSON.";
        }

        this.ResultBox.TextWrapping = comparison ? TextWrapping.Wrap : TextWrapping.NoWrap;
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            this.BuildTree(sender.Text);
        }
    }

    private void RequestTree_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        var content = args.InvokedItem is TreeViewNode node ? node.Content : args.InvokedItem;
        if (content is RequestDescriptor request)
        {
            this.Show(request);
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        this.SetBusy(true);
        await this._session.RefreshReferencesAsync();
        this.SetBusy(false);
        this.StatusLine.Text = $"{this._session.Scenes.Count} scenes and {this._session.Inputs.Count} inputs read.";
    }

    private async void InvokeButton_Click(object sender, RoutedEventArgs e) => await this.InvokeAsync();

    /// <summary>Shows a request, fills in its parameters and sends it, as the user would; for the smoke test.</summary>
    internal async Task SendForSmokeTest(RequestDescriptor request)
    {
        this.Show(request);
        this._form.FillForSmokeTest();
        await this.InvokeAsync();
    }

    /// <summary>Gets what the last request returned, as the client returned it; for the smoke test.</summary>
    internal string TypedResult => this._typed;

    /// <summary>Gets what OBS Studio sent as the response to the last request; for the smoke test.</summary>
    internal string RawResult => this._raw;

    /// <summary>Gets the comparison of the two; for the smoke test.</summary>
    internal string ComparisonResult => this._comparison;

    /// <summary>Gets what the page says about a request that failed, or null; for the smoke test.</summary>
    internal string? Problem => this.ProblemBar.IsOpen ? $"{this.ProblemBar.Title}: {this.ProblemBar.Message}" : null;

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

    private async Task InvokeAsync()
    {
        if (this._current is not { } request)
        {
            return;
        }

        ArgumentSet arguments;
        try
        {
            arguments = this._form.Collect();
        }
        catch (FormatException exception)
        {
            Fluent.ShowWarning(this.ProblemBar, "Check the parameters", exception.Message);
            return;
        }

        this.ProblemBar.IsOpen = false;
        this.SetBusy(true);
        var result = await this._session.InvokeAsync(request, arguments);
        this.SetBusy(false);

        var frame = this._session.Log.FindResponse(request.RequestType, result.StartSequence);
        this._typed = this.Render("result", () => result.Succeeded
            ? result.Value is null ? "(this request returns no data)" : JsonTools.Serialize(result.Value)
            : "(the request failed)");
        this._raw = frame is null ? "(the response of OBS Studio was not found in the log)" : JsonTools.Pretty(frame.Text);
        this._comparison = this.Render("comparison", () => Compare(result, frame));

        if (result.Error is { } error)
        {
            Fluent.ShowFailure(this.ProblemBar, error);
        }

        this.StatusLine.Text = $"{request.Name}({arguments}) \u2192 {(result.Succeeded ? "succeeded" : "failed")} in {result.Elapsed.TotalMilliseconds:N0} ms";
        this.ResultCard.Visibility = Visibility.Visible;
        this.ResultTabs.SelectedItem = !result.Succeeded && frame is not null ? this.RawTab : this.TypedTab;
        this.ShowTab();
    }

    private string Render(string what, Func<string> produce)
    {
        try
        {
            return produce();
        }
        catch (Exception exception)
        {
            this._session.Log.Note(LogLevel.Warning, $"The {what} of {this._current?.Name} could not be shown: {exception.Message}");
            return $"(the {what} could not be shown: {exception.Message})";
        }
    }

    private void BatchButton_Click(object sender, RoutedEventArgs e) => this.AddCurrentToBatch();

    /// <summary>Shows a request, fills in its parameters and puts it in the batch, as the user would; for the smoke test.</summary>
    /// <param name="request">The request.</param>
    /// <param name="optional">The optional parameters to give a value; all of them when empty.</param>
    /// <returns>Whether the request was added.</returns>
    internal bool AddToBatchForSmokeTest(RequestDescriptor request, params string[] optional)
    {
        this.Show(request);
        Func<ParameterDescriptor, bool>? chosen = null;
        if (optional.Length > 0)
        {
            chosen = parameter => optional.Contains(parameter.Name);
        }

        this._form.FillForSmokeTest(chosen);
        return this.AddCurrentToBatch();
    }

    private bool AddCurrentToBatch()
    {
        if (this._current is not { } request || request.AddToBatch is not { } add)
        {
            return false;
        }

        ArgumentSet arguments;
        try
        {
            arguments = this._form.Collect();
        }
        catch (FormatException exception)
        {
            Fluent.ShowWarning(this.ProblemBar, "Check the parameters", exception.Message);
            return false;
        }

        try
        {
            // The session builds the batch when it is sent; building one here tells the user at once if the client refuses the values.
            add(new RequestBatchMessage(), arguments);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or InvalidOperationException)
        {
            Fluent.ShowFailure(this.ProblemBar, exception);
            return false;
        }

        this.ProblemBar.IsOpen = false;
        this._session.Batch.Add(new BatchItem(request, arguments));
        this.StatusLine.Text = $"{request.Name} was put in the batch, which now has {this._session.Batch.Count} {(this._session.Batch.Count == 1 ? "request" : "requests")}.";
        return true;
    }

    private void ResultTabs_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args) => this.ShowTab();

    private async void CopyButton_Click(object sender, RoutedEventArgs e) => await ClipboardHelper.CopyAsync(this.CopyButton, this.ResultBox.Text);
}
