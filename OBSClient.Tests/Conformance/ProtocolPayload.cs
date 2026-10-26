namespace OBSStudioClient.Tests.Conformance
{
    using System.Text.Json;

    /// <summary>
    /// Checks the data of a request, as the client put it on the wire, against what obs-websocket documents for it.
    /// </summary>
    internal static class ProtocolPayload
    {
        /// <summary>
        /// Returns every field of <paramref name="data"/> that carries a value, by its (dot separated) name. A field
        /// that is null counts as absent, which is also how obs-websocket reads it. Only objects whose members the
        /// protocol documents individually, such as keyModifiers, are descended into.
        /// </summary>
        public static Dictionary<string, JsonElement> Flatten(JsonElement? data, IReadOnlyCollection<string> documentedFields)
        {
            var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            if (data is { ValueKind: JsonValueKind.Object } root)
            {
                Visit(root, string.Empty, documentedFields, result);
            }

            return result;
        }

        /// <summary>
        /// Returns what is wrong with the data a client sent for a request of the given type; empty when nothing is.
        /// </summary>
        public static IReadOnlyList<string> Validate(ProtocolSnapshot snapshot, string requestType, JsonElement? data)
        {
            var problems = new List<string>();
            if (!snapshot.Requests.TryGetValue(requestType, out var request))
            {
                problems.Add($"'{requestType}' is not a request of obs-websocket {snapshot.Version}.");
                return problems;
            }

            var documented = request.RequestFields.ToDictionary(field => field.Name, StringComparer.Ordinal);
            var present = Flatten(data, documented.Keys);

            foreach (var (path, value) in present)
            {
                if (!documented.TryGetValue(path, out var field))
                {
                    problems.Add($"{requestType}: sends '{path}', which the protocol does not define for this request.");
                    continue;
                }

                if (!TypeMatches(field.Type, value))
                {
                    problems.Add($"{requestType}: '{path}' is sent as {value.ValueKind}, but the protocol says {field.Type}.");
                }
                else if (path.EndsWith("Uuid", StringComparison.Ordinal) && value.ValueKind == JsonValueKind.String && !Guid.TryParse(value.GetString(), out _))
                {
                    problems.Add($"{requestType}: '{path}' is not a valid UUID.");
                }
            }

            foreach (var field in request.RequestFields.Where(field => !field.Optional))
            {
                // A required member of an optional object only has to be there when the object is.
                var separator = field.Name.LastIndexOf('.');
                if (separator >= 0 && !present.ContainsKey(field.Name[..separator]))
                {
                    continue;
                }

                if (!present.ContainsKey(field.Name))
                {
                    problems.Add($"{requestType}: the required field '{field.Name}' is not sent.");
                }
            }

            return problems;
        }

        private static void Visit(JsonElement element, string prefix, IReadOnlyCollection<string> documentedFields, Dictionary<string, JsonElement> result)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                {
                    continue;
                }

                var path = prefix.Length == 0 ? property.Name : prefix + "." + property.Name;
                result[path] = property.Value;

                if (property.Value.ValueKind == JsonValueKind.Object && documentedFields.Any(field => field.StartsWith(path + ".", StringComparison.Ordinal)))
                {
                    Visit(property.Value, path, documentedFields, result);
                }
            }
        }

        private static bool TypeMatches(string protocolType, JsonElement value)
        {
            return protocolType switch
            {
                "String" => value.ValueKind == JsonValueKind.String,
                "Number" => value.ValueKind == JsonValueKind.Number,
                "Boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
                "Object" => value.ValueKind == JsonValueKind.Object,
                _ when protocolType.StartsWith("Array", StringComparison.Ordinal) => value.ValueKind == JsonValueKind.Array,
                _ => true,
            };
        }
    }
}
