namespace OBSStudioClient.Tests.Unit
{
    using Xunit;

    /// <summary>
    /// Unit tests for <see cref="ObsClient.GetGeometry"/>, the Qt Base64 windowed-projector geometry helper.
    /// </summary>
    public class GetGeometryTests
    {
        [Fact]
        public void GetGeometry_ProducesValidBase64OfExpectedLength()
        {
            var geometry = ObsClient.GetGeometry(0, 10, 20, 800, 600, false, false, 1920);

            var bytes = Convert.FromBase64String(geometry);

            // magicNumber(4) + majorVersion(2) + minorVersion(2) + window(16) * 3 + screenNr(4) + maximized(1) + fullscreen(1) + screenWidth(4)
            Assert.Equal(66, bytes.Length);
        }

        [Fact]
        public void GetGeometry_SameInputs_AreDeterministic()
        {
            var first = ObsClient.GetGeometry(1, 0, 0, 1280, 720, false, false, 2560);
            var second = ObsClient.GetGeometry(1, 0, 0, 1280, 720, false, false, 2560);

            Assert.Equal(first, second);
        }

        [Fact]
        public void GetGeometry_DifferentPosition_ProducesDifferentOutput()
        {
            var atOrigin = ObsClient.GetGeometry(0, 0, 0, 800, 600, false, false, 1920);
            var offset = ObsClient.GetGeometry(0, 100, 100, 800, 600, false, false, 1920);

            Assert.NotEqual(atOrigin, offset);
        }
    }
}
