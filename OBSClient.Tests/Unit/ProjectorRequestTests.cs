namespace OBSStudioClient.Tests.Unit
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Messages;
    using OBSStudioClient.Tests.TestSupport;

    /// <summary>
    /// The projector requests are sent as they are given, and obs-websocket decides what a combination means: a monitorIndex of -1
    /// (also its default) is a windowed projector that can have a projectorGeometry, and a projectorGeometry is refused only next to a
    /// real monitor index (RequestHandler_Ui.cpp: OpenSourceProjector and OpenVideoMixProjector).
    /// </summary>
    public class ProjectorRequestTests
    {
        [Fact]
        public void SourceProjector_WithAGeometry_LeavesTheMonitorToObsStudio()
        {
            var batch = new RequestBatchMessage();

            batch.AddOpenSourceProjectorRequest("Camera", projectorGeometry: "geometry");

            var data = Assert.Single(batch.Requests).RequestData!.Value;
            Assert.Equal("Camera", data.GetProperty("sourceName").GetString());
            Assert.Equal("geometry", data.GetProperty("projectorGeometry").GetString());
            Assert.False(data.TryGetProperty("monitorIndex", out _));
        }

        [Fact]
        public void SourceProjector_OfACanvas_SendsTheCanvas()
        {
            var batch = new RequestBatchMessage();
            var canvasUuid = Guid.NewGuid();

            batch.AddOpenSourceProjectorRequest("Camera", projectorGeometry: "geometry", canvasUuid: canvasUuid);

            var data = Assert.Single(batch.Requests).RequestData!.Value;
            Assert.Equal(canvasUuid, data.GetProperty("canvasUuid").GetGuid());
            Assert.Equal("geometry", data.GetProperty("projectorGeometry").GetString());
        }

        [Fact]
        public void SourceProjector_WithMinusOneAndAGeometry_IsAWindowedProjector()
        {
            var batch = new RequestBatchMessage();

            batch.AddOpenSourceProjectorRequest("Camera", -1, "geometry");

            var data = Assert.Single(batch.Requests).RequestData!.Value;
            Assert.Equal(-1, data.GetProperty("monitorIndex").GetInt32());
            Assert.Equal("geometry", data.GetProperty("projectorGeometry").GetString());
        }

        [Fact]
        public void VideoMixProjector_WithAGeometryAndMinusOne_IsAWindowedProjector()
        {
            var batch = new RequestBatchMessage();

            batch.AddOpenVideoMixProjectorRequest(MixType.OBS_WEBSOCKET_VIDEO_MIX_TYPE_PREVIEW, -1, "geometry");

            var data = Assert.Single(batch.Requests).RequestData!.Value;
            Assert.Equal("OBS_WEBSOCKET_VIDEO_MIX_TYPE_PREVIEW", data.GetProperty("videoMixType").GetString());
            Assert.Equal(-1, data.GetProperty("monitorIndex").GetInt32());
            Assert.Equal("geometry", data.GetProperty("projectorGeometry").GetString());
        }

        [Fact]
        public async Task OpenSourceProjectorOnMonitor_SendsTheMonitor()
        {
            var sent = await ObsClientHarness.SendAsync(client => client.OpenSourceProjectorOnMonitor("Camera", 1));

            Assert.Equal("OpenSourceProjector", sent.RequestType);
            Assert.Equal(new[] { "monitorIndex", "sourceName" }, sent.Fields);
            Assert.Equal(1, sent["monitorIndex"].GetInt32());
        }

        [Fact]
        public async Task OpenSourceProjectorWindow_SendsTheGeometry()
        {
            var sent = await ObsClientHarness.SendAsync(client => client.OpenSourceProjectorWindow("Camera", "geometry"));

            Assert.Equal("OpenSourceProjector", sent.RequestType);
            Assert.Equal(new[] { "projectorGeometry", "sourceName" }, sent.Fields);
        }

        [Fact]
        public async Task OpenVideoMixProjectorOnMonitor_SendsTheMixAndTheMonitor()
        {
            var sent = await ObsClientHarness.SendAsync(client => client.OpenVideoMixProjectorOnMonitor(MixType.OBS_WEBSOCKET_VIDEO_MIX_TYPE_PROGRAM, 0));

            Assert.Equal("OpenVideoMixProjector", sent.RequestType);
            Assert.Equal(new[] { "monitorIndex", "videoMixType" }, sent.Fields);
            Assert.Equal("OBS_WEBSOCKET_VIDEO_MIX_TYPE_PROGRAM", sent["videoMixType"].GetString());
            Assert.Equal(0, sent["monitorIndex"].GetInt32());
        }

        [Fact]
        public async Task OpenVideoMixProjectorWindow_SendsTheMixAndTheGeometry()
        {
            var sent = await ObsClientHarness.SendAsync(client => client.OpenVideoMixProjectorWindow(MixType.OBS_WEBSOCKET_VIDEO_MIX_TYPE_MULTIVIEW, "geometry"));

            Assert.Equal(new[] { "projectorGeometry", "videoMixType" }, sent.Fields);
        }
    }
}
