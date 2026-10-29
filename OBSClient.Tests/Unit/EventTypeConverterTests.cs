namespace OBSStudioClient.Tests.Unit
{
    using OBSStudioClient.Enums;
    using System.Text.Json;
    using Xunit;

    /// <summary>
    /// Direct unit tests for <see cref="Converters.EventTypeConverter"/>: known event types round-trip, and
    /// unrecognized ones fall back to <see cref="EventType.Unknown"/> instead of throwing. See
    /// https://github.com/tinodo/obsclient/issues/12.
    /// </summary>
    public class EventTypeConverterTests
    {
        [Theory]
        [InlineData("ExitStarted", EventType.ExitStarted)]
        [InlineData("CanvasCreated", EventType.CanvasCreated)]
        [InlineData("CanvasRemoved", EventType.CanvasRemoved)]
        [InlineData("CanvasNameChanged", EventType.CanvasNameChanged)]
        [InlineData("CurrentProgramSceneChanged", EventType.CurrentProgramSceneChanged)]
        // The expected member is an object: a test method is public, and EventType is internal.
        public void Read_KnownEventTypeName_ReturnsMatchingEnumValue(string json, object expected)
        {
            var result = JsonSerializer.Deserialize<EventType>($"\"{json}\"");

            Assert.Equal(expected, (object)result);
        }

        [Theory]
        [InlineData("SomeBrandNewEventTypeFromAFutureObsWebsocketVersion")]
        [InlineData("")]
        [InlineData("exitstarted")] // wrong casing must not silently match a different member
        public void Read_UnrecognizedEventTypeName_ReturnsUnknownInsteadOfThrowing(string json)
        {
            var result = JsonSerializer.Deserialize<EventType>($"\"{json}\"");

            Assert.Equal(EventType.Unknown, result);
        }

        [Theory]
        [InlineData("\"5\"")] // numeric strings must not be parsed into an unrelated member
        [InlineData("\"ExitStarted, VendorEvent\"")] // flag-style lists must not be accepted
        [InlineData("5")]
        [InlineData("{\"nested\":[1,2,3]}")]
        [InlineData("[1,2,3]")]
        public void Read_NonNameToken_ReturnsUnknownInsteadOfThrowing(string rawJson)
        {
            var result = JsonSerializer.Deserialize<EventType>(rawJson);

            Assert.Equal(EventType.Unknown, result);
        }

        [Fact]
        public void Write_SerializesEnumValueAsItsName()
        {
            var json = JsonSerializer.Serialize(EventType.CanvasCreated);

            Assert.Equal("\"CanvasCreated\"", json);
        }
    }
}
