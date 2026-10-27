using System.Text;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OBSStudioClient.Exceptions;
using OBSStudioClient.TestApp.Analysis;
using OBSStudioClient.TestApp.Catalog;
using OBSStudioClient.TestApp.Controls;
using OBSStudioClient.TestApp.Services;
using Windows.ApplicationModel.DataTransfer;

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

            var text = new StringBuilder();
            text.AppendLine("What OBS Studio sent (responseData), compared with what the client returned.");
            text.AppendLine("A path starts with $, the response data. [] stands for every item of an array.");
            text.AppendLine();
            if (comparison.IsIdentical)
            {
                text.AppendLine("Identical: the client returns every member OBS Studio sent, as the same kind of value, and nothing else.");
                return text.ToString();
            }

            Section(text, "OBS Studio sent these, and the client does not return them", comparison.OnlyInObs);
            Section(text, "The client returns these, and OBS Studio did not send them", comparison.OnlyInClient);
            Section(text, "These are another kind of value", comparison.TypeDifferences);
            return text.ToString();
        }
        catch (JsonException exception)
        {
            return $"The messages could not be compared: {exception.Message}";
        }
    }

    private static void Section(StringBuilder text, string title, IReadOnlyList<string> lines)
    {
        if (lines.Count == 0)
        {
            return;
        }

        text.AppendLine($"{title} ({lines.Count}):");
        foreach (var line in lines)
        {
            text.AppendLine("  " + line);
        }

        text.AppendLine();
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
        this.UpdateButtons();
    }

    private void UpdateButtons()
    {
        var request = this._current;
        this.InvokeButton.IsEnabled = request?.Invoke is not null && this._session.IsConnected && !this._busy;
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

    private void ShowFailure(Exception error)
    {
        this.ProblemBar.Severity = error is ObsResponseException ? InfoBarSeverity.Warning : InfoBarSeverity.Error;
        this.ProblemBar.Title = error switch
        {
            ObsResponseException => "OBS Studio refused the request",
            ObsClientException => "The client could not complete the request",
            ArgumentException => "The client refused the arguments",
            _ => error.GetType().Name,
        };
        this.ProblemBar.Message = error is ObsResponseException response
            ? $"{response.ErrorCode} ({(int)response.ErrorCode}): {response.ErrorMessage ?? "no further explanation"}"
            : error.Message;
        this.ProblemBar.IsOpen = true;
    }

    private void ShowTab()
    {
        var selected = this.ResultTabs.SelectedItem;
        this.ResultBox.Text = ReferenceEquals(selected, this.RawTab)
            ? this._raw
            : ReferenceEquals(selected, this.ComparisonTab) ? this._comparison : this._typed;
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

    private async void InvokeButton_Click(object sender, RoutedEventArgs e)
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
            this.ProblemBar.Severity = InfoBarSeverity.Warning;
            this.ProblemBar.Title = "Check the parameters";
            this.ProblemBar.Message = exception.Message;
            this.ProblemBar.IsOpen = true;
            return;
        }

        this.ProblemBar.IsOpen = false;
        this.SetBusy(true);
        var result = await this._session.InvokeAsync(request, arguments);
        this.SetBusy(false);

        var frame = this._session.Log.FindResponse(request.Name, result.StartSequence);
        this._typed = result.Succeeded
            ? result.Value is null ? "(this request returns no data)" : JsonTools.Serialize(result.Value)
            : "(the request failed)";
        this._raw = frame is null ? "(the response of OBS Studio was not found in the log)" : JsonTools.Pretty(frame.Text);
        this._comparison = Compare(result, frame);

        if (result.Error is { } error)
        {
            this.ShowFailure(error);
        }

        this.StatusLine.Text = $"{request.Name}({arguments}) \u2192 {(result.Succeeded ? "succeeded" : "failed")} in {result.Elapsed.TotalMilliseconds:N0} ms";
        this.ResultCard.Visibility = Visibility.Visible;
        this.ResultTabs.SelectedItem = this.TypedTab;
        this.ShowTab();
    }

    private void ResultTabs_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args) => this.ShowTab();

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        var package = new DataPackage();
        package.SetText(this.ResultBox.Text);
        Clipboard.SetContent(package);
    }
}
