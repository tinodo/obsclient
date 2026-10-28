namespace OBSStudioClient.Tests.Live
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Tests.Conformance;
    using OBSStudioClient.TestApp.Analysis;
    using OBSStudioClient.TestApp.Catalog;
    using OBSStudioClient.TestApp.Services;
    using Xunit.Abstractions;

    /// <summary>
    /// What a real OBS Studio says, read with the client: the version, the requests that OBS Studio offers, and what each of the
    /// requests that only reads answers. These tests do not change anything in OBS Studio.
    /// </summary>
    [Collection("Live OBS")]
    public class LiveReadTests(ITestOutputHelper output)
    {
        // The answers that are checked at least: a sweep that exercised hardly any request would pass without proving anything.
        private const int LeastRequestsAnswered = 20;

        [LiveFact]
        public async Task ObsStudio_ReportsItsVersion_AndOffersOnlyRequestsThatTheClientHas()
        {
            await using var session = await LiveSession.ConnectAsync();

            var version = await session.Client.GetVersion();

            output.WriteLine($"OBS Studio {version.ObsVersion}, obs-websocket {version.ObsWebSocketVersion}, {version.Platform} ({version.PlatformDescription}).");
            Assert.Equal(1, version.RpcVersion);
            Assert.StartsWith("5.", version.ObsWebSocketVersion, StringComparison.Ordinal);
            Assert.NotEmpty(version.AvailableRequests);
            Assert.NotEmpty(version.SupportedImageFormats);

            var known = Enum.GetNames<RequestType>().ToHashSet(StringComparer.Ordinal);
            var missing = version.AvailableRequests
                .Where(name => !known.Contains(name) && !ObsStudioCaptureTests.IntentionallyUndocumented.Contains(name))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();
            Assert.True(missing.Count == 0, $"OBS Studio {version.ObsVersion} offers requests that the client does not have: {string.Join(", ", missing)}");
        }

        [LiveFact]
        public async Task EveryRequestThatOnlyReadsAndNeedsNoParameters_IsReadWithoutLosingWhatObsStudioSent()
        {
            await using var session = await LiveSession.ConnectAsync();
            var version = await session.Client.GetVersion();
            var offered = version.AvailableRequests.ToHashSet(StringComparer.Ordinal);

            var requests = RequestCatalog.All
                .Where(request => request.Invoke is not null
                    && request.Name.StartsWith("Get", StringComparison.Ordinal)
                    && request.Parameters.All(parameter => !parameter.Required)
                    && offered.Contains(request.RequestType))
                .ToList();

            var problems = new List<string>();
            var answered = new List<string>();
            var refused = new List<string>();
            foreach (var request in requests)
            {
                var from = session.Log.Entries.Count;
                object? returned;
                try
                {
                    returned = await request.Invoke!(session.Client, new ArgumentSet());
                }
                catch (ObsResponseException exception)
                {
                    // OBS Studio may refuse a request for what is going on, not for what the client did: there is no preview scene
                    // when Studio Mode is off, and no replay when the replay buffer did not run.
                    refused.Add($"{request.Name}: {exception.ErrorCode}");
                    continue;
                }
                catch (Exception exception) when (exception is ObsClientException or System.Text.Json.JsonException or InvalidOperationException or NotSupportedException)
                {
                    problems.Add($"{request.Name}: the client could not read the answer of OBS Studio: {exception.GetType().Name}: {exception.Message}");
                    continue;
                }

                var response = session.RawResponse(request.RequestType, from);
                if (response is null)
                {
                    problems.Add($"{request.Name}: the answer of OBS Studio was not found in the log.");
                    continue;
                }

                if (!response.Value.TryGetProperty("responseData", out var sent))
                {
                    // A request without data in its answer: the client gives nothing back either.
                    if (returned is not null)
                    {
                        problems.Add($"{request.Name}: OBS Studio sent no response data, the client returned {returned.GetType().Name}.");
                    }

                    answered.Add(request.Name);
                    continue;
                }

                if (returned is null)
                {
                    problems.Add($"{request.Name}: OBS Studio sent response data, the client returned nothing.");
                    continue;
                }

                if (request.ResponseField is { } field)
                {
                    // The method hands back the value of one member of the response.
                    if (!sent.TryGetProperty(field, out var member))
                    {
                        problems.Add($"{request.Name}: OBS Studio sent no member {field}, which the client returns.");
                        continue;
                    }

                    sent = member;
                }

                using var back = JsonTools.ToDocument(returned)!;
                var comparison = JsonShape.Compare(sent, back.RootElement);
                var type = returned.GetType();
                problems.AddRange(comparison.OnlyInObs
                    .Where(path => !ObsStudioCaptureTests.IsObsoleteAlias(type, path))
                    .Select(path => $"{request.Name}: OBS Studio sent {path}, the client does not give it back"));
                problems.AddRange(comparison.TypeDifferences.Select(difference => $"{request.Name}: {difference}"));
                answered.Add(request.Name);
            }

            output.WriteLine($"OBS Studio {version.ObsVersion}: {answered.Count} answers compared: {string.Join(", ", answered)}.");
            output.WriteLine($"OBS Studio refused {refused.Count} requests for what is going on in it: {string.Join("; ", refused)}.");

            Assert.True(answered.Count >= LeastRequestsAnswered, $"Only {answered.Count} of {requests.Count} requests were compared (at least {LeastRequestsAnswered} are expected): {string.Join("; ", refused)}");
            ConformanceAssert.NoProblems(problems, $"The client does not read what OBS Studio {version.ObsVersion} sent:");
        }
    }
}
