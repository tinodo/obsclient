namespace OBSStudioClient.Tests.Conformance
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Messages;
    using OBSStudioClient.Tests.TestSupport;
    using System.Reflection;

    /// <summary>
    /// Sends every event of the protocol to the client, shaped as the protocol documents it (every field, with a value
    /// of the documented type), and checks that it reaches the handler of that event, with the arguments type that is
    /// registered for it and with every documented value in place. <see cref="ProtocolCoverageTests"/> checks the
    /// names; this shows that the data can really be read, and that no event is dropped on the way.
    /// </summary>
    public class EventConformanceTests
    {
        private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(10);

        private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(3);

        [Fact]
        public async Task EveryEventOfTheProtocol_ReachesItsHandler_WithTheDocumentedData()
        {
            var protocol = ProtocolSnapshot.Current;
            var argumentTypes = Registries.ReadTypeMap<EventType>(typeof(EventMessage));
            await using var server = new FakeObsWebSocketServer();
            server.Start();
            using var client = new ObsClient();
            var connect = client.ConnectAsync(hostname: "127.0.0.1", port: server.Port);
            await server.HandshakeAsync().WithTimeoutAsync(HandshakeTimeout);
            Assert.True(await connect.WithTimeoutAsync(HandshakeTimeout), "Could not connect to the fake OBS server.");

            var problems = new List<string>();
            foreach (var (name, fields) in protocol.Events.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                var eventInfo = typeof(ObsClient).GetEvent(name);
                if (eventInfo?.EventHandlerType is null || !Enum.TryParse<EventType>(name, out var eventType) || !argumentTypes.TryGetValue(eventType, out var argumentType))
                {
                    problems.Add($"{name}: the client has no public event, or no arguments type, for it");
                    continue;
                }

                var properties = JsonNames.Properties(argumentType);
                var sample = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var field in fields)
                {
                    properties.TryGetValue(field.Name, out var property);
                    sample[field.Name] = ResponseSamples.SampleFor(field.Type, property?.PropertyType, field.Name);
                }

                var delivered = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
                var handler = CreateHandler(eventInfo.EventHandlerType, delivered);
                eventInfo.AddEventHandler(client, handler);
                try
                {
                    await server.SendEventAsync(name, sample.Count == 0 ? null : sample);
                    if (await Task.WhenAny(delivered.Task, Task.Delay(DeliveryTimeout)) != delivered.Task)
                    {
                        problems.Add($"{name}: did not reach its handler, so the client could not read the event as the protocol documents it");
                        continue;
                    }

                    problems.AddRange(Check(name, argumentType, await delivered.Task, fields, sample, properties));
                }
                finally
                {
                    eventInfo.RemoveEventHandler(client, handler);
                }
            }

            ConformanceAssert.NoProblems(problems, $"The events of the client do not match obs-websocket {protocol.Version}:");
        }

        private static IEnumerable<string> Check(
            string name,
            Type expectedArgumentType,
            object? arguments,
            IReadOnlyList<ProtocolField> fields,
            Dictionary<string, object?> sample,
            Dictionary<string, PropertyInfo> properties)
        {
            if (arguments is null || arguments.GetType() != expectedArgumentType)
            {
                yield return $"{name}: the handler received {arguments?.GetType().Name ?? "nothing"}, but {expectedArgumentType.Name} is registered for the event";
                yield break;
            }

            foreach (var field in fields)
            {
                if (!properties.TryGetValue(field.Name, out var property))
                {
                    yield return $"{name}: {expectedArgumentType.Name} has no property for the documented field '{field.Name}'";
                    continue;
                }

                var actual = property.GetValue(arguments);
                if (!ArrivedIntact(actual, sample[field.Name]))
                {
                    yield return $"{name}: the documented field '{field.Name}' was sent as '{sample[field.Name]}' but arrived as '{actual ?? "null"}'";
                }
            }
        }

        // Compares what was sent with what arrived. The protocol does not define the members of objects, so those are not compared.
        private static bool ArrivedIntact(object? actual, object? sample)
        {
            return sample switch
            {
                string text => actual is not null && string.Equals(actual.ToString(), text, StringComparison.OrdinalIgnoreCase),
                bool flag => actual is bool actualFlag && actualFlag == flag,
                int or long => actual is not null and not bool && (actual.GetType().IsPrimitive || actual is Enum) && Convert.ToDouble(actual) == Convert.ToDouble(sample),
                _ => true,
            };
        }

        private static Delegate CreateHandler(Type handlerType, TaskCompletionSource<object?> delivered)
        {
            if (handlerType == typeof(EventHandler))
            {
                return new EventHandler((_, arguments) => delivered.TrySetResult(arguments));
            }

            return (Delegate)typeof(EventConformanceTests)
                .GetMethod(nameof(CreateTypedHandler), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(handlerType.GetGenericArguments()[0])
                .Invoke(null, [delivered])!;
        }

        private static EventHandler<TArguments> CreateTypedHandler<TArguments>(TaskCompletionSource<object?> delivered)
        {
            return (_, arguments) => delivered.TrySetResult(arguments);
        }
    }
}
