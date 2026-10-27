namespace OBSStudioClient.Tests.TestAppTests
{
    using OBSStudioClient.TestApp.Services;
    using System.Text.Json;

    /// <summary>
    /// The test app shows and exports what OBS Studio sent: long values are left out of the text so that the rest stays readable,
    /// and secrets are hidden in what is exported.
    /// </summary>
    public class JsonToolsTests
    {
        [Fact]
        public void Collapse_LeavesAVeryLongStringOut_AndKeepsTheJsonValid()
        {
            var image = new string('A', 5000);
            var json = $$"""{"imageData":"{{image}}","width":1920}""";

            var collapsed = JsonTools.Collapse(json);

            Assert.DoesNotContain(image, collapsed, StringComparison.Ordinal);
            Assert.Contains("characters left out", collapsed, StringComparison.Ordinal);
            using var document = JsonDocument.Parse(collapsed);
            Assert.Equal(1920, document.RootElement.GetProperty("width").GetInt32());
        }

        [Fact]
        public void Collapse_LeavesShortTextAlone()
        {
            const string json = """{"a":"short"}""";

            Assert.Equal(json, JsonTools.Collapse(json));
        }

        [Theory]
        [InlineData("""{"authentication":"abc123","rpcVersion":1}""", """{"authentication":"***","rpcVersion":1}""")]
        [InlineData("""{"streamServiceSettings":{"key":"live_123","server":"rtmp://x"}}""", """{"streamServiceSettings":{"key":"***","server":"rtmp://x"}}""")]
        [InlineData("""{"password": "hunter2"}""", """{"password":"***"}""")]
        [InlineData("""{"apiKey":"a","stream_key":"b","bearerToken":"c","clientSecret":"d"}""", """{"apiKey":"***","stream_key":"***","bearerToken":"***","clientSecret":"***"}""")]
        [InlineData("""{"password":"say \"hi\" to me","after":1}""", """{"password":"***","after":1}""")]
        [InlineData("""{"keyId":"OBS_KEY_A","hotkeyName":"Start","keyModifiers":{"shift":true}}""", """{"keyId":"OBS_KEY_A","hotkeyName":"Start","keyModifiers":{"shift":true}}""")]
        public void Redact_HidesTheValuesOfSecrets_AndOnlyThose(string json, string expected)
        {
            Assert.Equal(expected.Replace(" ", string.Empty, StringComparison.Ordinal), JsonTools.Redact(json).Replace(" ", string.Empty, StringComparison.Ordinal));
        }

        [Fact]
        public void Pretty_IndentsJson_AndReturnsOtherTextAsItIs()
        {
            Assert.Contains(Environment.NewLine, JsonTools.Pretty("""{"a":1}"""), StringComparison.Ordinal);
            Assert.Equal("not json", JsonTools.Pretty("not json"));
        }

        [Fact]
        public void Serialize_ShowsTheNamesOfEnumerations_AndHandlesNull()
        {
            Assert.Equal("null", JsonTools.Serialize(null));
            Assert.Equal("\"Connected\"", JsonTools.Serialize(OBSStudioClient.Enums.ConnectionState.Connected));
        }
    }
}
