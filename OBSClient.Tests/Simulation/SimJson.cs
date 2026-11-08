namespace OBSStudioClient.Tests.Simulation
{
    using System.Globalization;
    using System.Text.Encodings.Web;
    using System.Text.Json;
    using System.Text.Json.Nodes;

    /// <summary>Builds the JSON of obs-websocket: compact, with the members of an object in alphabetical order, as obs-websocket writes it.</summary>
    internal static class SimJson
    {
        private static readonly JsonSerializerOptions Compact = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        /// <summary>
        /// Gets the order in which obs-websocket writes the members of an object: the order of the bytes of their UTF-8 names (its JSON library keeps an
        /// object in a sorted map of them), which is the order of their code points. It is not the order of <see cref="StringComparer.Ordinal"/>, which
        /// compares UTF-16 code units: a character beyond U+FFFF is two code units that start at U+D800, so it sorts before the characters from U+E000 to
        /// U+FFFF there, and after them here.
        /// </summary>
        public static IComparer<string> MemberOrder { get; } = new MemberOrderComparer();

        /// <summary>
        /// Reads a number from a node. A node that was parsed keeps the text of its number, and one that was made from a value of .NET keeps
        /// the value, and the second cannot be read as another kind of number by <see cref="JsonNode.GetValue{T}"/>: so this reads the text.
        /// </summary>
        public static bool TryGetNumber(JsonNode? node, out double value)
        {
            value = 0;
            if (node is not JsonValue || node.GetValueKind() != JsonValueKind.Number)
            {
                return false;
            }

            return double.TryParse(node.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>Reads a number that is an integer without a sign, which is what obs-websocket calls an unsigned number (up to 64 bits).</summary>
        public static bool TryGetUnsigned(JsonNode? node, out ulong value)
        {
            value = 0;
            if (node is not JsonValue || node.GetValueKind() != JsonValueKind.Number)
            {
                return false;
            }

            return ulong.TryParse(node.ToJsonString(), NumberStyles.None, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>Makes an object with the given members, in the order that obs-websocket writes them (see <see cref="MemberOrder"/>).</summary>
        public static JsonObject Obj(params (string Name, JsonNode? Value)[] members)
        {
            Array.Sort(members, static (left, right) => MemberOrder.Compare(left.Name, right.Name));
            var result = new JsonObject();
            foreach (var (name, value) in members)
            {
                result.Add(name, value);
            }

            return result;
        }

        /// <summary>Makes an array of the given items.</summary>
        public static JsonArray Arr(IEnumerable<JsonNode?> items)
        {
            var result = new JsonArray();
            foreach (var item in items)
            {
                result.Add(item);
            }

            return result;
        }

        /// <summary>Writes a node as the compact text that obs-websocket sends.</summary>
        public static string Serialize(JsonNode node)
        {
            return node.ToJsonString(Compact);
        }

        /// <summary>Copies a node with the members of every object in the order that obs-websocket writes them (see <see cref="MemberOrder"/>).</summary>
        public static JsonNode? Sorted(JsonNode? node)
        {
            switch (node)
            {
                case JsonObject obj:
                    var sorted = new JsonObject();
                    foreach (var member in obj.OrderBy(member => member.Key, MemberOrder))
                    {
                        sorted.Add(member.Key, Sorted(member.Value));
                    }

                    return sorted;
                case JsonArray array:
                    var copy = new JsonArray();
                    foreach (var item in array)
                    {
                        copy.Add(Sorted(item));
                    }

                    return copy;
                default:
                    return node?.DeepClone();
            }
        }

        private sealed class MemberOrderComparer : IComparer<string>
        {
            public int Compare(string? x, string? y)
            {
                if (ReferenceEquals(x, y))
                {
                    return 0;
                }

                if (x is null)
                {
                    return -1;
                }

                if (y is null)
                {
                    return 1;
                }

                var length = Math.Min(x.Length, y.Length);
                for (var index = 0; index < length; index++)
                {
                    var left = x[index];
                    var right = y[index];
                    if (left == right)
                    {
                        continue;
                    }

                    // The first code units that differ. A surrogate is half of a character beyond U+FFFF, which comes after every character that is
                    // not one; two surrogates, and two units that are not, order as their values do (which is the order of the code points).
                    var leftIsSurrogate = char.IsSurrogate(left);
                    var rightIsSurrogate = char.IsSurrogate(right);
                    if (leftIsSurrogate != rightIsSurrogate)
                    {
                        return leftIsSurrogate ? 1 : -1;
                    }

                    return left < right ? -1 : 1;
                }

                return x.Length.CompareTo(y.Length);
            }
        }
    }
}
