namespace OBSStudioClient.Tests.Conformance
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Messages;

    /// <summary>
    /// The response that obs-websocket sends for each request: every documented response field, with a value of the
    /// documented type. Feeding these to the client shows whether it can really read what the protocol promises.
    /// </summary>
    /// <remarks>
    /// The protocol documents the members of a nested object (a scene item, say) only in prose, so a nested object is
    /// sent empty; the client reads members that are missing as their default. An array carries one element of the
    /// documented element type, which is what shows that the client models the elements correctly. Where the protocol
    /// documents a string, but the client models the field (or element) as an enum or a Guid, the sample is a value
    /// that exists for that type; any other mismatch is left in the sample, so it fails.
    /// </remarks>
    internal sealed class ResponseSamples
    {
        private const string ArrayPrefix = "Array<";

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
                    response[field.Name] = SampleFor(field.Type, clientType, field.Name);
                }

                this._responses[name] = response;
            }
        }

        /// <summary>Gets the response data that obs-websocket sends for <paramref name="requestType"/>.</summary>
        public Dictionary<string, object?> For(string requestType)
        {
            return this._responses.TryGetValue(requestType, out var response) ? response : new Dictionary<string, object?>();
        }

        internal static object? SampleFor(string protocolType, Type? clientType, string fieldName)
        {
            var underlying = clientType is null ? null : Nullable.GetUnderlyingType(clientType) ?? clientType;

            // A Guid or an enum can only stand for a documented string (or number, for an enum): for any other documented
            // type the sample keeps the documented type, so a client that models the field that way fails to read it.
            if (underlying == typeof(Guid) && protocolType is ("String" or "Any"))
            {
                return Guid.NewGuid().ToString();
            }

            if (underlying is { IsEnum: true } && protocolType is ("String" or "Number" or "Any"))
            {
                var first = Enum.GetValues(underlying).GetValue(0)!;
                return protocolType == "Number" ? (object)Convert.ToInt64(first) : Enum.GetName(underlying, first);
            }

            if (protocolType.StartsWith(ArrayPrefix, StringComparison.Ordinal) && protocolType.EndsWith('>'))
            {
                var elementProtocolType = protocolType[ArrayPrefix.Length..^1];
                return new[] { SampleFor(elementProtocolType, ElementTypeOf(clientType), fieldName) };
            }

            return protocolType switch
            {
                "String" or "Any" => "sample",
                "Number" => 1,
                "Boolean" => true,
                "Object" => new Dictionary<string, object?>(),
                "Array" => Array.Empty<object>(),
                _ => throw new NotSupportedException($"The protocol type '{protocolType}' of the response field '{fieldName}' has no sample value; add one to {nameof(ResponseSamples)}."),
            };
        }

        private static Type? ElementTypeOf(Type? collectionType)
        {
            if (collectionType is null)
            {
                return null;
            }

            if (collectionType.IsArray)
            {
                return collectionType.GetElementType();
            }

            return new[] { collectionType }
                .Concat(collectionType.GetInterfaces())
                .Where(candidate => candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                .Select(candidate => candidate.GetGenericArguments()[0])
                .FirstOrDefault();
        }
    }
}
