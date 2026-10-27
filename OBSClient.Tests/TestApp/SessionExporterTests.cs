namespace OBSStudioClient.Tests.TestAppTests
{
    using Microsoft.Extensions.Logging;
    using OBSStudioClient.TestApp.Services;
    using System.Text.Json;

    /// <summary>
    /// The test app exports the log of a session for analysis, and people share what it exports: a secret must not get out in a
    /// line of the log, in whichever member of the line it is.
    /// </summary>
    public class SessionExporterTests
    {
        private static LogEntry Entry(LogKind kind, string title, string text) => new()
        {
            Time = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
            Kind = kind,
            Level = LogLevel.Debug,
            Title = title,
            Text = text,
        };

        [Fact]
        public void ALineThatIsRedacted_HidesASecretInTheTitle_AsWellAsInTheText()
        {
            var entry = Entry(
                LogKind.Client,
                """The response to request 1 could not be read: {"streamServiceSettings":{"key":"live_123"}}""",
                """The response to request 1 could not be read: {"streamServiceSettings":{"key":"live_123"}}""");

            var line = SessionExporter.ToLine(entry, redact: true);

            Assert.DoesNotContain("live_123", line, StringComparison.Ordinal);
            using var document = JsonDocument.Parse(line);
            Assert.Contains("***", document.RootElement.GetProperty("title").GetString(), StringComparison.Ordinal);
        }

        [Fact]
        public void ALineThatIsNotRedacted_KeepsTheTitleAsItIs()
        {
            var entry = Entry(LogKind.Client, """{"key":"live_123"}""", "text");

            var line = SessionExporter.ToLine(entry, redact: false);

            Assert.Contains("live_123", line, StringComparison.Ordinal);
        }

        [Fact]
        public void AMessageThatWentOverTheWire_IsWrittenAsAFrame_AndRedacted()
        {
            var entry = Entry(LogKind.Sent, "Identify", """{"d":{"authentication":"abc"},"op":1}""");

            var line = SessionExporter.ToLine(entry, redact: true);

            using var document = JsonDocument.Parse(line);
            Assert.Equal("***", document.RootElement.GetProperty("frame").GetProperty("d").GetProperty("authentication").GetString());
        }
    }
}
