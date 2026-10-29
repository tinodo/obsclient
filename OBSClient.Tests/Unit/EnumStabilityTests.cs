namespace OBSStudioClient.Tests.Unit
{
    using OBSStudioClient.Enums;
    using Xunit;

    /// <summary>
    /// C# embeds enum constants in consuming assemblies, so renumbering a published enum silently breaks
    /// already-compiled consumers after a library-only upgrade. These tests pin the numeric values of RequestType that
    /// shipped in 3.0.1; new members must be appended (or given explicit values), never inserted. (EventType is internal
    /// since 4.0.0: nothing can be compiled against it.)
    /// </summary>
    public class EnumStabilityTests
    {
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
