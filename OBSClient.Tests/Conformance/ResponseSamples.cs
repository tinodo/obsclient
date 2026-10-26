namespace OBSStudioClient.Tests.Conformance
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Messages;

    /// <summary>
    /// The response that obs-websocket sends for each request: every documented response field, with a value of the
    /// documented type. Feeding these to the client shows whether it can really read what the protocol promises.
    /// </summary>
    /// <remarks>
    /// The protocol documents the members of a nested object (a scene item, say) only in prose, so nested objects and
    /// arrays are sent empty; the client reads members that are missing as their default. Where the protocol documents a
    /// string, but the client models the field as an enum or a Guid, the sample is a value that exists for that type.
    /// </remarks>
    internal sealed class ResponseSamples
    {
        private readonly Dictionary<string, Dictionary<string, object?>> _responses = new(StringComparer.Ordinal);

        public ResponseSamples(ProtocolSnapshot protocol)
        {
            var responseTypes = Registries.ReadTypeMap<RequestType>(typeof(RequestResponseMessage));
            foreach (var (name, request) in protocol.Requests)
            {
                var clientTypes = Enum.TryParse<RequestType>(name, out var requestType) && responseTypes.TryGetValue(requestType, out var responseType)
                    ? JsonNames.PropertyTypes(responseType)
                    : new Dictionary<string, Type>(StringComparer.Ordinal);

                var response = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var field in request.ResponseFields)
                {
                    clientTypes.TryGetValue(field.Name, out var clientType);
                    response[field.Name] = SampleFor(field, clientType);
                }

                this._responses[name] = response;
            }
        }

        /// <summary>Gets the response data that obs-websocket sends for <paramref name="requestType"/>.</summary>
        public Dictionary<string, object?> For(string requestType)
        {
            return this._responses.TryGetValue(requestType, out var response) ? response : new Dictionary<string, object?>();
        }

        private static object? SampleFor(ProtocolField field, Type? clientType)
        {
            var underlying = clientType is null ? null : Nullable.GetUnderlyingType(clientType) ?? clientType;

            if (underlying == typeof(Guid))
            {
                return Guid.NewGuid().ToString();
            }

            if (underlying is { IsEnum: true })
            {
                var first = Enum.GetValues(underlying).GetValue(0)!;
                return field.Type == "String" ? Enum.GetName(underlying, first) : (object)Convert.ToInt64(first);
            }

            return field.Type switch
            {
                "String" or "Any" => "sample",
                "Number" => 1,
                "Boolean" => true,
                "Object" => new Dictionary<string, object?>(),
                _ when field.Type.StartsWith("Array", StringComparison.Ordinal) => Array.Empty<object>(),
                _ => throw new NotSupportedException($"The protocol type '{field.Type}' of the response field '{field.Name}' has no sample value; add one to {nameof(ResponseSamples)}."),
            };
        }
    }
}
