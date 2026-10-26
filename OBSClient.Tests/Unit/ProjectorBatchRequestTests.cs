namespace OBSStudioClient.Tests.Unit
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Messages;

    /// <summary>
    /// obs-websocket treats a monitorIndex of -1 as "no monitor" (a windowed projector) and rejects a projectorGeometry
    /// only next to a real monitor index (RequestHandler_Ui.cpp: OpenSourceProjector and OpenVideoMixProjector).
    /// </summary>
    public class ProjectorBatchRequestTests
    {
        [Fact]
        public void SourceProjector_WithAGeometryAndTheDefaultMonitorIndex_IsAWindowedProjector()
        {
            var batch = new RequestBatchMessage();

            batch.AddOpenSourceProjectorRequest("Camera", projectorGeometry: "geometry");

            var data = Assert.Single(batch.Requests).RequestData!.Value;
            Assert.Equal(-1, data.GetProperty("monitorIndex").GetInt32());
            Assert.Equal("geometry", data.GetProperty("projectorGeometry").GetString());
        }

        [Fact]
        public void SourceProjector_OfACanvas_WithAGeometryAndTheDefaultMonitorIndex_IsAWindowedProjector()
        {
            var batch = new RequestBatchMessage();
            var canvasUuid = Guid.NewGuid();

            batch.AddOpenSourceProjectorRequest("Camera", canvasUuid, projectorGeometry: "geometry");

            var data = Assert.Single(batch.Requests).RequestData!.Value;
            Assert.Equal(canvasUuid, data.GetProperty("canvasUuid").GetGuid());
            Assert.Equal("geometry", data.GetProperty("projectorGeometry").GetString());
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void SourceProjector_WithAMonitorAndAGeometry_IsRefused(int monitorIndex)
        {
            var batch = new RequestBatchMessage();

            Assert.Throws<ArgumentException>(() => batch.AddOpenSourceProjectorRequest("Camera", monitorIndex, "geometry"));

            Assert.Empty(batch.Requests);
        }

        [Fact]
        public void VideoMixProjector_WithAGeometryAndMinusOne_IsAWindowedProjector()
        {
            var batch = new RequestBatchMessage();

            batch.AddOpenVideoMixProjectorRequest(MixType.OBS_WEBSOCKET_VIDEO_MIX_TYPE_PREVIEW, -1, "geometry");

            var data = Assert.Single(batch.Requests).RequestData!.Value;
            Assert.Equal(-1, data.GetProperty("monitorIndex").GetInt32());
            Assert.Equal("geometry", data.GetProperty("projectorGeometry").GetString());
        }

        [Fact]
        public void VideoMixProjector_WithAMonitorAndAGeometry_IsRefused()
        {
            var batch = new RequestBatchMessage();

            Assert.Throws<ArgumentException>(() => batch.AddOpenVideoMixProjectorRequest(MixType.OBS_WEBSOCKET_VIDEO_MIX_TYPE_PROGRAM, 2, "geometry"));

            Assert.Empty(batch.Requests);
        }

        [Fact]
        public void VideoMixProjector_WithNeitherAMonitorNorAGeometry_IsRefused()
        {
            var batch = new RequestBatchMessage();

            Assert.Throws<ArgumentException>(() => batch.AddOpenVideoMixProjectorRequest(MixType.OBS_WEBSOCKET_VIDEO_MIX_TYPE_MULTIVIEW, null, null));

            Assert.Empty(batch.Requests);
        }
    }
}
