namespace OBSStudioClient.Tests.Live
{
    using System.Text.Json;

    /// <summary>
    /// Compares what OBS Studio sent with what the client gives back by value. The shape comparison of the test app (<c>JsonShape</c>)
    /// finds a member that is lost and a member that became another kind of value; this finds a member whose value is not the one that
    /// OBS Studio sent, for instance two members that were swapped, or a number that was changed on the way.
    /// </summary>
    internal static class JsonValues
    {
        // The client holds a number that OBS Studio sends as a double in a float, which is what the protocol says it is: the values
        // are the same when they are the same to the precision of a float (about seven digits).
        private const double RelativeTolerance = 1e-6;

        /// <summary>Finds where what the client gives back differs in value from what OBS Studio sent.</summary>
        /// <param name="sent">What OBS Studio sent.</param>
        /// <param name="returned">What the client gives back, as JSON. A member that OBS Studio sent and that is not here is not reported: the shape comparison does that.</param>
        /// <param name="nullsReadAsEmpty">
        /// The paths of members that the client holds as an empty object or array when OBS Studio sends null, because the one who uses the
        /// client is never given null for them. For such a path a null is the same as an empty value, and nothing else is: a value that
        /// the client made up is still reported.
        /// </param>
        /// <returns>The differences, as texts that start with the path of the value; nothing when the values are the same.</returns>
        public static IReadOnlyList<string> Differences(JsonElement sent, JsonElement returned, IReadOnlyCollection<string>? nullsReadAsEmpty = null)
        {
            var found = new List<string>();
            Compare("$", sent, returned, nullsReadAsEmpty, found);
            return found;
        }

        private static void Compare(string path, JsonElement sent, JsonElement returned, IReadOnlyCollection<string>? nullsReadAsEmpty, List<string> found)
        {
            switch (sent.ValueKind)
            {
                case JsonValueKind.Object:
                    if (returned.ValueKind != JsonValueKind.Object)
                    {
                        found.Add($"{path}: OBS Studio sent an object, the client gave {returned.ValueKind}");
                        return;
                    }

                    foreach (var member in sent.EnumerateObject())
                    {
                        if (returned.TryGetProperty(member.Name, out var other))
                        {
                            Compare($"{path}.{member.Name}", member.Value, other, nullsReadAsEmpty, found);
                        }
                    }

                    break;

                case JsonValueKind.Array:
                    if (returned.ValueKind != JsonValueKind.Array)
                    {
                        found.Add($"{path}: OBS Studio sent an array, the client gave {returned.ValueKind}");
                        return;
                    }

                    if (sent.GetArrayLength() != returned.GetArrayLength())
                    {
                        found.Add($"{path}: OBS Studio sent {sent.GetArrayLength()} items, the client gave {returned.GetArrayLength()}");
                        return;
                    }

                    var index = 0;
                    foreach (var (item, other) in sent.EnumerateArray().Zip(returned.EnumerateArray()))
                    {
                        Compare($"{path}[{index++}]", item, other, nullsReadAsEmpty, found);
                    }

                    break;

                case JsonValueKind.String:
                    if (returned.ValueKind != JsonValueKind.String || !string.Equals(sent.GetString(), returned.GetString(), StringComparison.Ordinal))
                    {
                        found.Add($"{path}: OBS Studio sent {Describe(sent)}, the client gave {Describe(returned)}");
                    }

                    break;

                case JsonValueKind.Number:
                    if (returned.ValueKind != JsonValueKind.Number || !SameNumber(sent.GetDouble(), returned.GetDouble()))
                    {
                        found.Add($"{path}: OBS Studio sent {sent.GetRawText()}, the client gave {Describe(returned)}");
                    }

                    break;

                case JsonValueKind.True:
                case JsonValueKind.False:
                    if (returned.ValueKind != sent.ValueKind)
                    {
                        found.Add($"{path}: OBS Studio sent {sent.GetRawText()}, the client gave {Describe(returned)}");
                    }

                    break;

                case JsonValueKind.Null:
                    if (returned.ValueKind != JsonValueKind.Null && !(nullsReadAsEmpty?.Contains(path) == true && IsEmpty(returned)))
                    {
                        found.Add($"{path}: OBS Studio sent null, the client gave {Describe(returned)}");
                    }

                    break;
            }
        }

        private static bool IsEmpty(JsonElement element) => element.ValueKind switch
        {
            JsonValueKind.Object => !element.EnumerateObject().Any(),
            JsonValueKind.Array => element.GetArrayLength() == 0,
            _ => false,
        };

        private static bool SameNumber(double sent, double returned) =>
            Math.Abs(sent - returned) <= RelativeTolerance * Math.Max(1.0, Math.Max(Math.Abs(sent), Math.Abs(returned)));

        // Never the content of a string: the settings of a stream hold a key, and a failure of a test ends up in logs that are shared.
        private static string Describe(JsonElement element) => element.ValueKind switch
        {
            JsonValueKind.String => $"a string of {element.GetString()?.Length} characters",
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => element.GetRawText(),
            _ => element.ValueKind.ToString().ToLowerInvariant(),
        };
    }
}
