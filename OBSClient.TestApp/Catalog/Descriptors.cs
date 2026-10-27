using OBSStudioClient.Enums;
using OBSStudioClient.Messages;

namespace OBSStudioClient.TestApp.Catalog;

/// <summary>The kind of editor that a parameter of a request needs.</summary>
internal enum ParameterKind
{
    /// <summary>Free text.</summary>
    Text,

    /// <summary>A whole number.</summary>
    Integer,

    /// <summary>A number with decimals.</summary>
    Number,

    /// <summary>True or false.</summary>
    Boolean,

    /// <summary>A UUID.</summary>
    Guid,

    /// <summary>One of the members of an enumeration.</summary>
    Enum,

    /// <summary>A scene, by name or by UUID.</summary>
    Scene,

    /// <summary>An input, by name or by UUID.</summary>
    Input,

    /// <summary>A source (a scene or an input), by name or by UUID.</summary>
    Source,

    /// <summary>A JSON object.</summary>
    JsonObject,

    /// <summary>Any JSON value.</summary>
    Json,
}

/// <summary>A parameter of a request, as the method of the client takes it.</summary>
/// <param name="Name">The name of the parameter of the method.</param>
/// <param name="Kind">The kind of editor the parameter needs.</param>
/// <param name="TypeName">The C# type of the parameter, for display.</param>
/// <param name="Required">Whether the call needs a value.</param>
/// <param name="Description">What the protocol says about the parameter.</param>
/// <param name="EnumType">The enumeration, for <see cref="ParameterKind.Enum"/>.</param>
/// <param name="Example">What the editor of a JSON parameter starts with.</param>
/// <param name="SendsNullWhenOmitted">Whether leaving the value out sends null, which the protocol gives a meaning.</param>
internal sealed record ParameterDescriptor(
    string Name,
    ParameterKind Kind,
    string TypeName,
    bool Required,
    string Description,
    Type? EnumType = null,
    string? Example = null,
    bool SendsNullWhenOmitted = false);

/// <summary>A request of the protocol, with the code that sends it.</summary>
/// <param name="Name">The name of the request, and of the method of the client.</param>
/// <param name="Category">The category of the protocol documentation.</param>
/// <param name="Description">What the protocol says about the request.</param>
/// <param name="Since">The version of obs-websocket that introduced the request.</param>
/// <param name="Parameters">The parameters of the method, in order.</param>
/// <param name="Invoke">Calls the method; null for a request that cannot be sent on its own.</param>
/// <param name="AddToBatch">Adds the request to a batch; null for a shortcut of the client, which is a method of its own rather than a request of the protocol.</param>
/// <param name="WireType">The type of the request that goes over the wire, when it is not <paramref name="Name"/>: a shortcut sends another request.</param>
internal sealed record RequestDescriptor(
    string Name,
    string Category,
    string Description,
    string Since,
    IReadOnlyList<ParameterDescriptor> Parameters,
    Func<ObsClient, ArgumentSet, Task<object?>>? Invoke,
    Action<RequestBatchMessage, ArgumentSet>? AddToBatch,
    string? WireType = null)
{
    /// <summary>Gets the type of the request that goes over the wire, which is how OBS Studio names its response.</summary>
    public string RequestType => this.WireType ?? this.Name;

    /// <inheritdoc/>
    public override string ToString() => this.Name;
}

/// <summary>An event of the protocol, with the code that observes it.</summary>
/// <param name="Name">The name of the event, and of the event of the client.</param>
/// <param name="Category">The category of the protocol documentation.</param>
/// <param name="Description">What the protocol says about the event.</param>
/// <param name="Since">The version of obs-websocket that introduced the event.</param>
/// <param name="Subscription">The event subscription that the event needs.</param>
/// <param name="Subscribe">Hands every occurrence of the event, with its event arguments (or null), to a sink.</param>
internal sealed record EventDescriptor(
    string Name,
    string Category,
    string Description,
    string Since,
    EventSubscriptions Subscription,
    Action<ObsClient, Action<string, object?>> Subscribe);
