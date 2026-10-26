namespace OBSStudioClient.Tests
{
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Tests.TestSupport;
    using System.Text.Json;
    using Xunit;

    /// <summary>
    /// Verifies the request/response round trip, including error propagation, timeouts, and the new
    /// GetCanvasList request introduced in obs-websocket 5.7.0.
    /// </summary>
    public class RequestResponseTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private static async Task<ObsClient> ConnectAsync(FakeObsWebSocketServer server)
        {
            server.Start();
            var client = new ObsClient
            {
                // The default 500ms is tight for a loaded CI machine running both the net8.0 and net10.0 test
                // TFMs in parallel; these tests care about correctness, not about exercising the default timeout.
                RequestTimeout = 5000,
            };
            var connectTask = client.ConnectAsync(hostname: "127.0.0.1", port: server.Port);
            await server.HandshakeAsync().WithTimeoutAsync(Timeout);
            await connectTask.WithTimeoutAsync(Timeout);
            return client;
        }

        [Fact]
        public async Task GetCanvasList_DeserializesNestedCanvasObjectsCorrectly()
        {
            await using var server = new FakeObsWebSocketServer();
            using var client = await ConnectAsync(server);

            var requestTask = client.GetCanvasList();
            var (requestType, requestId, _) = await server.ReceiveRequestAsync(Timeout);
            Assert.Equal("GetCanvasList", requestType);

            var canvasUuid = Guid.NewGuid();
            await server.SendRequestSuccessAsync(requestType, requestId, new
            {
                canvases = new[]
                {
                    new
                    {
                        canvasName = "Main Canvas",
                        canvasUuid,
                        canvasFlags = new { MAIN = true, ACTIVATE = true, MIX_AUDIO = true, SCENE_REF = false, EPHEMERAL = false },
                        canvasVideoSettings = new { fpsNumerator = 60, fpsDenominator = 1, baseWidth = 1920, baseHeight = 1080, outputWidth = 1920, outputHeight = 1080 },
                    },
                },
            });

            var canvases = await requestTask.WithTimeoutAsync(Timeout);

            var canvas = Assert.Single(canvases);
            Assert.Equal("Main Canvas", canvas.CanvasName);
            Assert.Equal(canvasUuid, canvas.CanvasUuid);
            Assert.True(canvas.CanvasFlags.Main);
            Assert.False(canvas.CanvasFlags.SceneRef);
            Assert.Equal(1920, canvas.CanvasVideoSettings.BaseWidth);
            Assert.Equal(60, canvas.CanvasVideoSettings.FpsNumerator);
        }

        [Fact]
        public async Task SendRequest_ErrorResponse_ThrowsObsResponseExceptionWithCode()
        {
            await using var server = new FakeObsWebSocketServer();
            using var client = await ConnectAsync(server);

            var requestTask = client.GetSceneList();
            var (requestType, requestId, _) = await server.ReceiveRequestAsync(Timeout);
            await server.SendRequestErrorAsync(requestType, requestId, 600, "Resource not found");

            var exception = await Assert.ThrowsAsync<ObsResponseException>(() => requestTask.WithTimeoutAsync(Timeout));
            Assert.Equal(Enums.RequestStatusCode.ResourceNotFound, exception.ErrorCode);
            Assert.Equal("Resource not found", exception.ErrorMessage);
        }

        [Fact]
        public async Task SendRequest_NoResponseWithinTimeout_ThrowsTimeoutException()
        {
            await using var server = new FakeObsWebSocketServer();
            using var client = await ConnectAsync(server);
            client.RequestTimeout = 300;

            var requestTask = client.GetSceneList();
            await server.ReceiveRequestAsync(Timeout); // consume the request; deliberately never respond.

            await Assert.ThrowsAsync<TimeoutException>(() => requestTask.WithTimeoutAsync(Timeout));
        }

        [Fact]
        public async Task SceneListResponse_ScenesOnNonMainCanvas_DeserializeWithNullSceneIndex()
        {
            // Regression test: scenes returned for a non-main canvas have a null sceneIndex (OBS has no
            // ordering concept there); Scene.SceneIndex must be nullable to support this.
            await using var server = new FakeObsWebSocketServer();
            using var client = await ConnectAsync(server);

            var canvasUuid = Guid.NewGuid();
            var requestTask = client.GetSceneList(canvasUuid);
            var (requestType, requestId, requestData) = await server.ReceiveRequestAsync(Timeout);
            Assert.Equal("GetSceneList", requestType);
            Assert.Equal(canvasUuid, requestData!.Value.GetProperty("canvasUuid").GetGuid());

            var sceneUuid = Guid.NewGuid();
            await server.SendRequestSuccessAsync(requestType, requestId, new
            {
                currentProgramSceneName = (string?)null,
                currentProgramSceneUuid = (Guid?)null,
                currentPreviewSceneName = (string?)null,
                currentPreviewSceneUuid = (Guid?)null,
                scenes = new[] { new { sceneName = "Vertical Scene", sceneUuid, sceneIndex = (int?)null } },
            });

            var response = await requestTask.WithTimeoutAsync(Timeout);
            var scene = Assert.Single(response.Scenes);
            Assert.Null(scene.SceneIndex);
            Assert.Equal("Vertical Scene", scene.SceneName);
            Assert.Equal(sceneUuid, scene.SceneUuid);
        }
    }
}
