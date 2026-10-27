using System.Text.Json;

namespace OBSStudioClient.TestApp.Analysis;

/// <summary>
/// The differences in shape between what OBS Studio sent and what the client made of it. Paths look like
/// <c>$.scenes[].sceneName</c>: <c>[]</c> stands for every item of an array.
/// </summary>
/// <param name="OnlyInObs">Members that OBS Studio sent and the client does not give back.</param>
/// <param name="OnlyInClient">Members that the client gives back and OBS Studio did not send.</param>
/// <param name="TypeDifferences">Members that are a different kind of JSON value on both sides.</param>
internal sealed record ShapeComparison(
    IReadOnlyList<string> OnlyInObs,
    IReadOnlyList<string> OnlyInClient,
    IReadOnlyList<string> TypeDifferences)
{
    /// <summary>Gets a value indicating whether the client gives back exactly the shape that OBS Studio sent.</summary>
    public bool IsIdentical => this.OnlyInObs.Count == 0 && this.OnlyInClient.Count == 0 && this.TypeDifferences.Count == 0;
}

/// <summary>What has been seen at one path of a JSON document, over any number of documents.</summary>
internal sealed class PathObservation
{
    /// <summary>Gets the kinds of JSON value that were seen (<c>string</c>, <c>number</c>, <c>boolean</c>, <c>null</c>, <c>object</c> or <c>array</c>).</summary>
    public SortedSet<string> Types { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the number of times a value was seen.</summary>
    public int Seen { get; set; }
}

/// <summary>Compares and describes the shape of JSON: its members and the kind of value of each, not the values.</summary>
internal static class JsonShape
{
    /// <summary>Names the kind of a JSON value.</summary>
    public static string TypeName(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => "string",
        JsonValueKind.Number => "number",
        JsonValueKind.True or JsonValueKind.False => "boolean",
        JsonValueKind.Null => "null",
        JsonValueKind.Object => "object",
        JsonValueKind.Array => "array",
        _ => "undefined",
    };

    /// <summary>
    /// Compares what OBS Studio sent with what the client gives back. A null on either side is "no value", not a difference
    /// of type. A call that returns the one field of a response gives that value, not an object that holds it: the field is
    /// compared in that case.
    /// </summary>
    /// <param name="fromObs">The response data as OBS Studio sent it.</param>
    /// <param name="fromClient">The value the client gave back, as JSON.</param>
    public static ShapeComparison Compare(JsonElement fromObs, JsonElement fromClient)
    {
        if (fromObs.ValueKind == JsonValueKind.Object && fromClient.ValueKind != JsonValueKind.Object)
        {
            var members = fromObs.EnumerateObject().ToList();
            if (members.Count == 1)
            {
                fromObs = members[0].Value;
            }
        }

        var onlyInObs = new SortedSet<string>(StringComparer.Ordinal);
        var onlyInClient = new SortedSet<string>(StringComparer.Ordinal);
        var typeDifferences = new SortedSet<string>(StringComparer.Ordinal);
        Walk("$", fromObs, fromClient, onlyInObs, onlyInClient, typeDifferences);
        return new ShapeComparison(onlyInObs.ToList(), onlyInClient.ToList(), typeDifferences.ToList());
    }

    /// <summary>Records the path and the kind of value of every member of a document.</summary>
    /// <param name="path">The path of the value, <c>$</c> for the document itself.</param>
    /// <param name="element">The value.</param>
    /// <param name="into">The observations to add to.</param>
    public static void Observe(string path, JsonElement element, IDictionary<string, PathObservation> into)
    {
        if (!into.TryGetValue(path, out var observation))
        {
            observation = new PathObservation();
            into[path] = observation;
        }

        observation.Types.Add(TypeName(element));
        observation.Seen++;

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var member in element.EnumerateObject())
                {
                    Observe($"{path}.{member.Name}", member.Value, into);
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    Observe(path + "[]", item, into);
                }

                break;
        }
    }

    private static void Walk(
        string path,
        JsonElement fromObs,
        JsonElement fromClient,
        ISet<string> onlyInObs,
        ISet<string> onlyInClient,
        ISet<string> typeDifferences)
    {
        if (fromObs.ValueKind != fromClient.ValueKind && TypeName(fromObs) != TypeName(fromClient))
        {
            if (fromObs.ValueKind != JsonValueKind.Null && fromClient.ValueKind != JsonValueKind.Null)
            {
                typeDifferences.Add($"{path}: OBS Studio sent {TypeName(fromObs)}, the client gives {TypeName(fromClient)}");
            }

            return;
        }

        switch (fromObs.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var member in fromObs.EnumerateObject())
                {
                    var memberPath = $"{path}.{member.Name}";
                    if (fromClient.TryGetProperty(member.Name, out var counterpart))
                    {
                        Walk(memberPath, member.Value, counterpart, onlyInObs, onlyInClient, typeDifferences);
                    }
                    else
                    {
                        onlyInObs.Add($"{memberPath} ({TypeName(member.Value)})");
                    }
                }

                foreach (var member in fromClient.EnumerateObject())
                {
                    if (!fromObs.TryGetProperty(member.Name, out _))
                    {
                        onlyInClient.Add($"{path}.{member.Name} ({TypeName(member.Value)})");
                    }
                }

                break;
            case JsonValueKind.Array:
                var obsItems = fromObs.EnumerateArray().ToArray();
                var clientItems = fromClient.EnumerateArray().ToArray();
                if (obsItems.Length != clientItems.Length)
                {
                    typeDifferences.Add($"{path}: OBS Studio sent {obsItems.Length} items, the client gives {clientItems.Length}");
                }

                for (var index = 0; index < Math.Min(obsItems.Length, clientItems.Length); index++)
                {
                    Walk(path + "[]", obsItems[index], clientItems[index], onlyInObs, onlyInClient, typeDifferences);
                }

                break;
        }
    }
}
