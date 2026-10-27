namespace OBSStudioClient.Tests.Unit
{
    using OBSStudioClient.Enums;
    using Xunit;

    /// <summary>
    /// C# embeds enum constants in consuming assemblies, so renumbering a published enum silently breaks
    /// already-compiled consumers after a library-only upgrade. These tests pin the numeric values that shipped
    /// in 3.0.1; new members must be appended (or given explicit values), never inserted.
    /// </summary>
    public class EnumStabilityTests
    {
        [Theory]
        [InlineData(EventType.Unknown, -1)]
        [InlineData(EventType.ExitStarted, 0)]
        [InlineData(EventType.InputActiveStateChanged, 19)]
        [InlineData(EventType.SourceFilterEnableStateChanged, 38)]
        [InlineData(EventType.ScreenshotSaved, 56)]
        [InlineData(EventType.CanvasCreated, 57)]
        [InlineData(EventType.CanvasRemoved, 58)]
        [InlineData(EventType.CanvasNameChanged, 59)]
        public void EventType_PublishedValues_AreStable(EventType member, int expected)
        {
            Assert.Equal(expected, (int)member);
        }

        [Theory]
        [InlineData(RequestType.GetVersion, 0)]
        [InlineData(RequestType.GetInputMute, 48)]
        [InlineData(RequestType.GetSceneItemLocked, 97)]
        [InlineData(RequestType.OpenSourceProjector, 145)]
        [InlineData(RequestType.GetCanvasList, 146)]
        public void RequestType_PublishedValues_AreStable(RequestType member, int expected)
        {
            Assert.Equal(expected, (int)member);
        }
    }
}
