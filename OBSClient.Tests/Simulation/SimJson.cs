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

        /// <summary>Reads a number that is an integer without a sign, which is what obs-websocket calls an unsigned number.</summary>
        public static bool TryGetUnsigned(JsonNode? node, out long value)
        {
            value = 0;
            if (node is not JsonValue || node.GetValueKind() != JsonValueKind.Number)
            {
                return false;
            }

            return long.TryParse(node.ToJsonString(), NumberStyles.None, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>Makes an object with the given members, in alphabetical order.</summary>
        public static JsonObject Obj(params (string Name, JsonNode? Value)[] members)
        {
            Array.Sort(members, static (left, right) => string.CompareOrdinal(left.Name, right.Name));
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

        /// <summary>Copies a node with the members of every object in alphabetical order (obs-websocket keeps the members of an object in that order).</summary>
        public static JsonNode? Sorted(JsonNode? node)
        {
            switch (node)
            {
                case JsonObject obj:
                    var sorted = new JsonObject();
                    foreach (var member in obj.OrderBy(member => member.Key, StringComparer.Ordinal))
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
    }
}
