namespace OBSStudioClient.Tests.Conformance
{
    using System.Text.Json;
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Messages;
    using OBSStudioClient.TestApp.Analysis;
    using OBSStudioClient.TestApp.Services;

    /// <summary>
    /// What a real OBS Studio answered (captured read-only, with what identifies the machine replaced: see the files under
    /// Fixtures), read by the client. The protocol documents the members of a response, and the other conformance tests hold the
    /// client to that; this holds it to what OBS Studio really sends, including the nested objects, which the protocol only
    /// describes in prose. A member that OBS Studio sent and the client does not give back is a member the client lost, and a
    /// member that became another kind of value is a mistake of the model.
    /// </summary>
    public class ObsStudioCaptureTests
    {
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
                    var comparison = JsonShape.Compare(sent, returned.RootElement);
                    problems.AddRange(comparison.OnlyInObs.Select(path => $"{name}: OBS Studio sent {path}, the client does not give it back"));
                    problems.AddRange(comparison.TypeDifferences.Select(difference => $"{name}: {difference}"));
                }

                checkedResponses++;
            }

            Assert.True(checkedResponses > 0, $"{capture} holds no responses.");
            ConformanceAssert.NoProblems(problems, $"The client does not read what OBS Studio sent in {capture}:");
        }
    }
}
