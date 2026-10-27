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
        public void Collapse_LeavesAVeryLongStringWithAnEscapedQuoteOut_AsAWhole()
        {
            var json = $$"""{"settings":"{{new string('a', 2500)}}\"quoted\"{{new string('b', 600)}}","width":1920}""";

            var collapsed = JsonTools.Collapse(json);

            Assert.Contains("characters left out", collapsed, StringComparison.Ordinal);
            using var document = JsonDocument.Parse(collapsed);
            Assert.StartsWith("<", document.RootElement.GetProperty("settings").GetString(), StringComparison.Ordinal);
            Assert.Equal(1920, document.RootElement.GetProperty("width").GetInt32());
        }

        [Fact]
        public void Collapse_LeavesALongStretchWithoutStringsAlone_ThatLiesBetweenTwoShortOnes()
        {
            var numbers = string.Join(',', Enumerable.Repeat("0.123456", 400));
            var json = $$"""{"a":"x","values":[{{numbers}}],"b":"y"}""";

            Assert.Equal(json, JsonTools.Collapse(json));
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

        [Theory]
        [InlineData("""The response could not be read: {"op":7,"d":{"password":"hunter2-is-the-sec""", "hunter2")]
        [InlineData("""The response could not be read: {"streamServiceSettings":{"key":"live_123\""", "live_123")]
        [InlineData("""The response could not be read: {"authentication":"abc\u00""", "abc")]
        [InlineData("""The response could not be read: {"password":"hunter2""", "hunter2")]
        public void Redact_HidesASecretThatIsCutOff_AtTheEndOfAPreview(string text, string secret)
        {
            var redacted = JsonTools.Redact(text);

            Assert.DoesNotContain(secret, redacted, StringComparison.Ordinal);
            Assert.Contains("***", redacted, StringComparison.Ordinal);
        }

        [Fact]
        public void Redact_StopsAnUnfinishedSecret_AtTheEndOfTheLine()
        {
            var text = "The response could not be read: {\"password\":\"hunter2" + Environment.NewLine + "and the next line";

            var redacted = JsonTools.Redact(text);

            Assert.DoesNotContain("hunter2", redacted, StringComparison.Ordinal);
            Assert.EndsWith("and the next line", redacted, StringComparison.Ordinal);
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
