namespace OBSStudioClient.Tests.Conformance
{
    using System.Reflection;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Messages;
    using OBSStudioClient.Tests.TestSupport;
    using OBSStudioClient.TestApp.Analysis;
    using OBSStudioClient.TestApp.Catalog;
    using OBSStudioClient.TestApp.Services;

    /// <summary>
    /// What a real OBS Studio sent (captured, with what identifies the machine replaced: see the files under Fixtures), read by
    /// the client: the responses to requests, and the events. The protocol documents the members of a response and of an event,
    /// and the other conformance tests hold the client to that; this holds it to what OBS Studio really sends, including the
    /// nested objects, which the protocol only describes in prose. A member that OBS Studio sent and the client does not give
    /// back is a member the client lost, and a member that became another kind of value is a mistake of the model.
    /// </summary>
    public class ObsStudioCaptureTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        // Requests that obs-websocket registers, but that its source marks as "Intentionally undocumented" (checked in 5.7.6: the
        // private settings of a source and of a scene item). They are not part of the protocol, so the client leaves them out.
        private static readonly string[] IntentionallyUndocumented =
        [
            "GetSceneItemPrivateSettings",
            "GetSourcePrivateSettings",
            "SetSceneItemPrivateSettings",
            "SetSourcePrivateSettings",
        ];

        // A path of a member of the response itself, as the comparison writes it: $.name (kind)
        private static readonly Regex TopLevelMember = new(@"^\$\.([A-Za-z0-9_]+) \(", RegexOptions.Compiled);

        public static IEnumerable<object[]> Captures() =>
            typeof(ObsStudioCaptureTests).Assembly.GetManifestResourceNames()
                .Where(name => name.StartsWith("obs-capture-", StringComparison.Ordinal))
                .OrderBy(name => name, StringComparer.Ordinal)
                .Select(name => new object[] { name });

        [Theory]
        [MemberData(nameof(Captures))]
        public void EveryResponseOfARealObsStudio_IsReadWithoutLosingWhatObsStudioSent(string capture)
        {
            using var stream = typeof(ObsStudioCaptureTests).Assembly.GetManifestResourceStream(capture)!;
            using var document = JsonDocument.Parse(stream);
            var problems = new List<string>();
            var checkedResponses = 0;

            foreach (var response in document.RootElement.GetProperty("responses").EnumerateArray())
            {
                var requestType = response.GetProperty("requestType").GetString()!;
                var name = response.TryGetProperty("label", out var label) ? $"{requestType} ({label.GetString()})" : requestType;
                var sent = response.GetProperty("responseData");

                var message = new RequestResponseMessage(
                    Enum.Parse<RequestType>(requestType),
                    "id",
                    new RequestStatus(true, RequestStatusCode.Success, null),
                    sent.Clone());
                try
                {
                    message.OnDeserialized();
                }
                catch (Exception exception)
                {
                    problems.Add($"{name}: the client could not read it: {exception.Message}");
                    continue;
                }

                if (message.ResponseData is null)
                {
                    problems.Add($"{name}: the client read no response data.");
                    continue;
                }

                JsonDocument returned;
                try
                {
                    returned = JsonTools.ToDocument(message.ResponseData)!;
                }
                catch (Exception exception) when (exception is NotSupportedException or JsonException or InvalidOperationException)
                {
                    problems.Add($"{name}: what the client read could not be written as JSON: {exception.Message}");
                    continue;
                }

                using (returned)
                {
                    var responseType = message.ResponseData.GetType();
                    var comparison = JsonShape.Compare(sent, returned.RootElement);
                    problems.AddRange(comparison.OnlyInObs
                        .Where(path => !IsObsoleteAlias(responseType, path))
                        .Select(path => $"{name}: OBS Studio sent {path}, the client does not give it back"));
                    problems.AddRange(comparison.TypeDifferences.Select(difference => $"{name}: {difference}"));
                }

                checkedResponses++;
            }

            Assert.True(checkedResponses > 0, $"{capture} holds no responses.");
            ConformanceAssert.NoProblems(problems, $"The client does not read what OBS Studio sent in {capture}:");
        }

        [Theory]
        [MemberData(nameof(Captures))]
        public void EveryRequestThatARealObsStudioOffers_IsInTheClient(string capture)
        {
            using var stream = typeof(ObsStudioCaptureTests).Assembly.GetManifestResourceStream(capture)!;
            using var document = JsonDocument.Parse(stream);
            var version = document.RootElement.GetProperty("responses").EnumerateArray()
                .First(response => response.GetProperty("requestType").GetString() == "GetVersion")
                .GetProperty("responseData");
            var offered = version.GetProperty("availableRequests").EnumerateArray().Select(name => name.GetString()!).ToList();
            var known = Enum.GetNames<RequestType>().ToHashSet(StringComparer.Ordinal);

            var missing = offered
                .Where(name => !known.Contains(name) && !IntentionallyUndocumented.Contains(name))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();

            Assert.True(missing.Count == 0, $"OBS Studio offers requests that the client does not have ({capture}): {string.Join(", ", missing)}");
        }

        [Theory]
        [MemberData(nameof(Captures))]
        public async Task EveryEventOfARealObsStudio_IsRaisedWithoutLosingWhatObsStudioSent(string capture)
        {
            using var stream = typeof(ObsStudioCaptureTests).Assembly.GetManifestResourceStream(capture)!;
            using var document = JsonDocument.Parse(stream);
            if (!document.RootElement.TryGetProperty("events", out var events) || events.GetArrayLength() == 0)
            {
                return;
            }

            await using var server = new FakeObsWebSocketServer();
            server.Start();
            using var client = new ObsClient { RequestTimeout = 5000 };
            var heard = new List<(string Name, object? Arguments)>();
            RequestCatalog.Subscribe(client, (name, arguments) =>
            {
                lock (heard)
                {
                    heard.Add((name, arguments));
                }
            });
            var connect = client.ConnectAsync(hostname: "127.0.0.1", port: server.Port);
            await server.HandshakeAsync().WithTimeoutAsync(Timeout);
            await connect.WithTimeoutAsync(Timeout);

            var problems = new List<string>();
            foreach (var captured in events.EnumerateArray())
            {
                var eventType = captured.GetProperty("eventType").GetString()!;
                int before;
                lock (heard)
                {
                    before = heard.Count;
                }

                var data = new Dictionary<string, object?>
                {
                    ["eventType"] = eventType,
                    ["eventIntent"] = captured.TryGetProperty("eventIntent", out var intent) ? (object)intent : 1,
                };
                var hasData = captured.TryGetProperty("eventData", out var eventData) && eventData.ValueKind == JsonValueKind.Object;
                if (hasData)
                {
                    data["eventData"] = eventData;
                }

                // The client passes on one volume meter every 100 ms: those of a capture are sent far enough apart.
                if (eventType == nameof(EventType.InputVolumeMeters))
                {
                    await Task.Delay(150);
                }

                await server.SendRawAsync(JsonSerializer.Serialize(new { op = 5, d = data }));

                var raised = await AsyncTestHelpers.WaitUntilAsync(() => { lock (heard) { return heard.Count > before; } }, TimeSpan.FromSeconds(3));
                if (!raised)
                {
                    problems.Add($"{eventType}: the client raised no event.");
                    continue;
                }

                object? arguments;
                lock (heard)
                {
                    arguments = heard[before].Arguments;
                }

                if (!hasData)
                {
                    continue;
                }

                if (arguments is null)
                {
                    problems.Add($"{eventType}: OBS Studio sent event data, the client gave no arguments.");
                    continue;
                }

                using var returned = JsonTools.ToDocument(arguments)!;
                var comparison = JsonShape.Compare(eventData, returned.RootElement);
                var argumentsType = arguments.GetType();
                problems.AddRange(comparison.OnlyInObs
                    .Where(path => !IsObsoleteAlias(argumentsType, path))
                    .Select(path => $"{eventType}: OBS Studio sent {path}, the client does not give it back"));
                problems.AddRange(comparison.TypeDifferences.Select(difference => $"{eventType}: {difference}"));
            }

            ConformanceAssert.NoProblems(problems, $"The client does not raise what OBS Studio sent in {capture}:");
        }

        // The deprecated names that obs-websocket still sends (currentProgramSceneName next to sceneName) are kept by the client
        // as obsolete properties that are computed from the property that replaced them: they are read, but not written back.
        private static bool IsObsoleteAlias(Type responseType, string path)
        {
            var match = TopLevelMember.Match(path);
            return match.Success
                && responseType.GetProperties().Any(property =>
                    property.GetCustomAttribute<ObsoleteAttribute>() is not null
                    && JsonNamingPolicy.CamelCase.ConvertName(property.Name) == match.Groups[1].Value);
        }
    }
}
