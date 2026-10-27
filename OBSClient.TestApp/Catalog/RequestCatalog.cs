namespace OBSStudioClient.TestApp.Catalog;

/// <summary>Every request and event the test app knows: the generated ones and the ones that are written by hand.</summary>
internal static class RequestCatalog
{
    /// <summary>Gets all requests, in the order of the protocol documentation, and then the shortcuts.</summary>
    public static IReadOnlyList<RequestDescriptor> All { get; } = [.. ProtocolCatalog.Requests, .. ManualCatalog.Requests];

    /// <summary>Gets the categories, in the order in which they appear.</summary>
    public static IReadOnlyList<string> Categories { get; } = [.. All.Select(request => request.Category).Distinct()];

    /// <summary>Gets the events, in the order of the protocol documentation.</summary>
    public static IReadOnlyList<EventDescriptor> Events => ProtocolCatalog.Events;

    /// <summary>Gets a request by name.</summary>
    public static RequestDescriptor? Find(string? name) => name is null ? null : All.FirstOrDefault(request => request.Name == name);

    /// <summary>Gets the category of an event, or an empty text for an event that is not known.</summary>
    public static string CategoryOfEvent(string name) => Events.FirstOrDefault(descriptor => descriptor.Name == name)?.Category ?? string.Empty;

    /// <summary>Lets the sink hear about every event of the client.</summary>
    /// <param name="client">The client.</param>
    /// <param name="sink">Receives the name of the event, and its event arguments (or null).</param>
    public static void Subscribe(ObsClient client, Action<string, object?> sink)
    {
        foreach (var descriptor in Events)
        {
            descriptor.Subscribe(client, sink);
        }
    }
}
