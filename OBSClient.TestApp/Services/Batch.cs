using OBSStudioClient.Messages;
using OBSStudioClient.TestApp.Catalog;

namespace OBSStudioClient.TestApp.Services;

/// <summary>A request that was put in the batch, with the values of its parameters.</summary>
public sealed class BatchItem
{
    internal BatchItem(RequestDescriptor request, ArgumentSet arguments)
    {
        this.Request = request;
        this.Arguments = arguments;
    }

    /// <summary>Gets the name of the request.</summary>
    public string Title => this.Request.Name;

    /// <summary>Gets the values of the parameters, as the call received them.</summary>
    public string Summary => this.Arguments.Values.Count == 0 ? "no parameters" : this.Arguments.ToString();

    internal RequestDescriptor Request { get; }

    internal ArgumentSet Arguments { get; }
}

/// <summary>What happened when a batch was sent.</summary>
/// <param name="Succeeded">Whether OBS Studio answered the batch; the requests in it may still have failed.</param>
/// <param name="Responses">The responses of the individual requests, as the client returns them.</param>
/// <param name="Elapsed">How long it took.</param>
/// <param name="Error">What went wrong, when nothing came back.</param>
/// <param name="StartSequence">The position in the log where the batch started.</param>
/// <param name="RequestId">The identifier of the batch, to find the answer of OBS Studio in the log.</param>
internal sealed record BatchResult(bool Succeeded, RequestResponseMessage[]? Responses, TimeSpan Elapsed, Exception? Error, long StartSequence, string? RequestId);
