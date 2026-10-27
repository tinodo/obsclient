namespace OBSStudioClient.Tests.Conformance
{
    using System.Text.Json;

    /// <summary>A field of a request, a response or an event, as documented by obs-websocket.</summary>
    internal sealed record ProtocolField(string Name, string Type, bool Optional);

    /// <summary>The documented request and response fields of one obs-websocket request.</summary>
    internal sealed record ProtocolRequest(IReadOnlyList<ProtocolField> RequestFields, IReadOnlyList<ProtocolField> ResponseFields);

    /// <summary>
    /// A compact snapshot of the obs-websocket protocol (written by tools/protocol/generate.py), which the
    /// conformance tests compare the client against so it can no longer silently fall behind the protocol.
    /// </summary>
    internal sealed class ProtocolSnapshot
    {
        private const string ResourceName = "protocol.schema.json";

        private static readonly Lazy<ProtocolSnapshot> _current = new(Load);

        private ProtocolSnapshot(
            string version,
            Dictionary<string, ProtocolRequest> requests,
            Dictionary<string, IReadOnlyList<ProtocolField>> events,
            Dictionary<string, IReadOnlyDictionary<string, long?>> enums)
        {
            this.Version = version;
            this.Requests = requests;
            this.Events = events;
            this.Enums = enums;
        }

        public static ProtocolSnapshot Current => _current.Value;

        /// <summary>Gets the obs-websocket version the snapshot was taken from.</summary>
        public string Version { get; }

        public IReadOnlyDictionary<string, ProtocolRequest> Requests { get; }

        public IReadOnlyDictionary<string, IReadOnlyList<ProtocolField>> Events { get; }

        /// <summary>Gets the enums of the protocol: the value of each identifier, or null when it has no numeric value.</summary>
        public IReadOnlyDictionary<string, IReadOnlyDictionary<string, long?>> Enums { get; }

        private static ProtocolSnapshot Load()
        {
            using var stream = typeof(ProtocolSnapshot).Assembly.GetManifestResourceStream(ResourceName)
                ?? throw new InvalidOperationException($"The embedded resource '{ResourceName}' was not found.");
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;

            var requests = new Dictionary<string, ProtocolRequest>(StringComparer.Ordinal);
            foreach (var request in root.GetProperty("requests").EnumerateObject())
            {
                requests[request.Name] = new ProtocolRequest(
                    ReadFields(request.Value.GetProperty("request")),
                    ReadFields(request.Value.GetProperty("response")));
            }

            var events = new Dictionary<string, IReadOnlyList<ProtocolField>>(StringComparer.Ordinal);
            foreach (var protocolEvent in root.GetProperty("events").EnumerateObject())
            {
                events[protocolEvent.Name] = ReadFields(protocolEvent.Value);
            }

            var enums = new Dictionary<string, IReadOnlyDictionary<string, long?>>(StringComparer.Ordinal);
            foreach (var enumeration in root.GetProperty("enums").EnumerateObject())
            {
                var identifiers = new Dictionary<string, long?>(StringComparer.Ordinal);
                foreach (var identifier in enumeration.Value.EnumerateObject())
                {
                    identifiers[identifier.Name] = identifier.Value.ValueKind == JsonValueKind.Number ? identifier.Value.GetInt64() : null;
                }

                enums[enumeration.Name] = identifiers;
            }

            return new ProtocolSnapshot(root.GetProperty("obsWebSocketVersion").GetString()!, requests, events, enums);
        }

        private static List<ProtocolField> ReadFields(JsonElement fields)
        {
            return fields.EnumerateArray()
                .Select(field => new ProtocolField(
                    field.GetProperty("name").GetString()!,
                    field.GetProperty("type").GetString()!,
                    field.TryGetProperty("optional", out var optional) && optional.GetBoolean()))
                .ToList();
        }
    }
}
