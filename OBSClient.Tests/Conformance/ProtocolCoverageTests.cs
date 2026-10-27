namespace OBSStudioClient.Tests.Conformance
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Messages;
    using System.Reflection;

    /// <summary>
    /// Verifies that the client knows every request, event and enum identifier that obs-websocket documents, and
    /// the other way around. These are the things that silently go missing when the protocol evolves and the client
    /// does not. The reference is the snapshot in Protocol/protocol.schema.json, which tools/protocol/generate.py writes
    /// (from the protocol definition, with the patches of overlay.toml applied) together with most of the code that is
    /// checked here: the generated parts hold by construction, so these tests mostly guard the hand-written parts and
    /// the generator itself.
    /// </summary>
    public class ProtocolCoverageTests
    {
        private static ProtocolSnapshot Protocol => ProtocolSnapshot.Current;

        [Fact]
        public void RequestType_MatchesTheProtocol()
        {
            var client = Enum.GetNames<RequestType>().ToHashSet(StringComparer.Ordinal);
            var problems = new List<string>();
            problems.AddRange(Protocol.Requests.Keys.Where(name => !client.Contains(name)).OrderBy(name => name, StringComparer.Ordinal)
                .Select(name => $"the protocol defines the request '{name}', which RequestType does not"));
            problems.AddRange(client.Where(name => !Protocol.Requests.ContainsKey(name)).OrderBy(name => name, StringComparer.Ordinal)
                .Select(name => $"RequestType defines '{name}', which the protocol does not"));

            ConformanceAssert.NoProblems(problems, $"RequestType does not match the requests of obs-websocket {Protocol.Version}:");
        }

        [Fact]
        public void EventType_MatchesTheProtocol()
        {
            // Unknown is the client's own fallback for event types it does not recognize.
            var client = Enum.GetNames<EventType>().Where(name => name != nameof(EventType.Unknown)).ToHashSet(StringComparer.Ordinal);
            var problems = new List<string>();
            problems.AddRange(Protocol.Events.Keys.Where(name => !client.Contains(name)).OrderBy(name => name, StringComparer.Ordinal)
                .Select(name => $"the protocol defines the event '{name}', which EventType does not"));
            problems.AddRange(client.Where(name => !Protocol.Events.ContainsKey(name)).OrderBy(name => name, StringComparer.Ordinal)
                .Select(name => $"EventType defines '{name}', which the protocol does not"));

            ConformanceAssert.NoProblems(problems, $"EventType does not match the events of obs-websocket {Protocol.Version}:");
        }

        [Fact]
        public void EveryRequest_HasAResponseType_ThatExposesTheDocumentedFields()
        {
            var responseTypes = Registries.ReadTypeMap<RequestType>(typeof(RequestResponseMessage));
            var problems = new List<string>();

            foreach (var (name, request) in Protocol.Requests.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                if (!Enum.TryParse<RequestType>(name, out var requestType) || !responseTypes.TryGetValue(requestType, out var responseType))
                {
                    problems.Add($"{name}: no response type is registered in RequestResponseMessage");
                    continue;
                }

                var exposed = JsonNames.Of(responseType);
                foreach (var field in request.ResponseFields)
                {
                    if (!exposed.Contains(field.Name))
                    {
                        problems.Add($"{name}: the response field '{field.Name}' is not exposed by {responseType.Name}");
                    }
                }
            }

            ConformanceAssert.NoProblems(problems, $"The responses of the client do not cover obs-websocket {Protocol.Version}:");
        }

        [Fact]
        public void EveryEvent_CanBeDispatched_AndItsArgumentsExposeTheDocumentedFields()
        {
            var argumentTypes = Registries.ReadTypeMap<EventType>(typeof(EventMessage));
            var problems = new List<string>();

            foreach (var (name, fields) in Protocol.Events.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                if (!Enum.TryParse<EventType>(name, out var eventType) || !argumentTypes.TryGetValue(eventType, out var argumentType))
                {
                    problems.Add($"{name}: no event arguments type is registered in EventMessage");
                    continue;
                }

                var exposed = JsonNames.Of(argumentType);
                foreach (var field in fields)
                {
                    if (!exposed.Contains(field.Name))
                    {
                        problems.Add($"{name}: the data field '{field.Name}' is not exposed by {argumentType.Name}");
                    }
                }

                // Events reach their handlers through a field of ObsClient that is named after the event.
                var backingField = typeof(ObsClient).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
                if (backingField is null || backingField.FieldType.BaseType != typeof(MulticastDelegate))
                {
                    problems.Add($"{name}: ObsClient has no event with this name, so the event is never delivered");
                    continue;
                }

                var handlerArgumentType = backingField.FieldType.GetMethod("Invoke")!.GetParameters()[1].ParameterType;
                if (!handlerArgumentType.IsAssignableFrom(argumentType))
                {
                    problems.Add($"{name}: the event of ObsClient takes {handlerArgumentType.Name}, but {argumentType.Name} is what is delivered");
                }
            }

            ConformanceAssert.NoProblems(problems, $"The events of the client do not cover obs-websocket {Protocol.Version}:");
        }

        [Fact]
        public void EventSubscriptions_MatchTheProtocol() => AssertEnumMatches<EventSubscriptions>("EventSubscription");

        [Fact]
        public void RequestStatusCode_MatchesTheProtocol() => AssertEnumMatches<RequestStatusCode>("RequestStatus");

        [Fact]
        public void OpCode_MatchesTheProtocol() => AssertEnumMatches<OpCode>("WebSocketOpCode");

        [Fact]
        public void RequestBatchExecutionType_MatchesTheProtocol() => AssertEnumMatches<RequestBatchExecutionType>("RequestBatchExecutionType");

        [Fact]
        public void WebSocketCloseCode_ContainsEveryCodeOfTheProtocol() => AssertEnumMatches<WebSocketCloseCode>("WebSocketCloseCode", clientMayHaveMore: true);

        [Fact]
        public void ObsMediaInputAction_MatchesTheProtocol() => AssertEnumMatches<ObsMediaInputAction>("ObsMediaInputAction");

        [Fact]
        public void ObsOutputState_MatchesTheProtocol() => AssertEnumMatches<ObsOutputState>("ObsOutputState");

        private static void AssertEnumMatches<TEnum>(string protocolEnum, bool clientMayHaveMore = false)
            where TEnum : struct, Enum
        {
            var protocol = Protocol.Enums[protocolEnum];
            var client = Enum.GetNames<TEnum>().ToDictionary(Normalize, name => Convert.ToInt64(Enum.Parse<TEnum>(name)), StringComparer.Ordinal);
            var problems = new List<string>();

            foreach (var (name, expected) in protocol)
            {
                if (!client.TryGetValue(Normalize(name), out var actual))
                {
                    problems.Add($"the protocol defines '{name}', which {typeof(TEnum).Name} does not");
                }
                else if (expected is { } value && value != actual)
                {
                    problems.Add($"'{name}' is {actual} in {typeof(TEnum).Name}, but {value} in the protocol");
                }
            }

            if (!clientMayHaveMore)
            {
                var known = protocol.Keys.Select(Normalize).ToHashSet(StringComparer.Ordinal);
                problems.AddRange(client.Keys.Where(name => !known.Contains(name)).Select(name => $"{typeof(TEnum).Name} defines '{name}', which the protocol does not"));
            }

            ConformanceAssert.NoProblems(problems, $"{typeof(TEnum).Name} does not match the {protocolEnum} enum of obs-websocket {Protocol.Version}:");
        }

        // The protocol names some identifiers differently (OBS_WEBSOCKET_OUTPUT_STARTED) than C# convention does.
        private static string Normalize(string identifier)
        {
            return new string(identifier.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        }
    }
}
