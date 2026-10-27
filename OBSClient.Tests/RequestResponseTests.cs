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

            // The safety timeout is applied to the wait, not to the assertion: only an exception thrown by the
            // client itself may satisfy the assertion, so a client whose own timeout is broken fails this test.
            var finished = await Task.WhenAny(requestTask, Task.Delay(Timeout));
            Assert.Same(requestTask, finished);
            await Assert.ThrowsAsync<TimeoutException>(() => requestTask);
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

        [Fact]
        public async Task SetInputSettings_Completes_AndTheConnectionStaysUsable()
        {
            // Regression test for https://github.com/tinodo/obsclient/issues/17: no response type was registered for
            // SetInputSettings, so its (data-less) response made the receive loop throw, which closed the connection
            // while ConnectionState still said Connected.
            await using var server = new FakeObsWebSocketServer();
            using var client = await ConnectAsync(server);

            var set = client.SetInputSettings("Browser", new Dictionary<string, object> { ["url"] = "https://example.com" });
            var (requestType, requestId, requestData) = await server.ReceiveRequestAsync(Timeout);
            Assert.Equal("SetInputSettings", requestType);
            Assert.Equal("https://example.com", requestData!.Value.GetProperty("inputSettings").GetProperty("url").GetString());
            await server.SendRequestSuccessAsync(requestType, requestId); // OBS Studio sends no response data for this request
            await set.WithTimeoutAsync(Timeout, "SetInputSettings never completed.");

            var next = client.GetStudioModeEnabled();
            var (nextType, nextId, _) = await server.ReceiveRequestAsync(Timeout);
            await server.SendRequestSuccessAsync(nextType, nextId, new { studioModeEnabled = false });
            Assert.False(await next.WithTimeoutAsync(Timeout, "The connection stopped working after SetInputSettings."));
            Assert.Equal(Enums.ConnectionState.Connected, client.ConnectionState);
        }

        [Fact]
        public async Task GetInputAudioMonitorType_ReturnsTheMonitorTypeOfTheInput()
        {
            await using var server = new FakeObsWebSocketServer();
            using var client = await ConnectAsync(server);

            var request = client.GetInputAudioMonitorType("Mic");
            var (requestType, requestId, _) = await server.ReceiveRequestAsync(Timeout);
            await server.SendRequestSuccessAsync(requestType, requestId, new { monitorType = "OBS_MONITORING_TYPE_MONITOR_ONLY" });

            Assert.Equal(Enums.MonitorType.OBS_MONITORING_TYPE_MONITOR_ONLY, await request.WithTimeoutAsync(Timeout));
        }

        [Fact]
        public async Task GetSceneList_WithoutCanvas_SendsNoRequestData()
        {
            // The no-argument overload keeps the wire format it always had; a canvasUuid is only sent when asked for.
            await using var server = new FakeObsWebSocketServer();
            using var client = await ConnectAsync(server);

            var requestTask = client.GetSceneList();
            var (requestType, requestId, requestData) = await server.ReceiveRequestAsync(Timeout);
            await server.SendRequestSuccessAsync(requestType, requestId, new { scenes = Array.Empty<object>() });
            await requestTask.WithTimeoutAsync(Timeout);

            Assert.Equal("GetSceneList", requestType);
            Assert.True(requestData is null || requestData.Value.ValueKind == JsonValueKind.Null, $"Unexpected request data: {requestData}");
        }

        [Fact]
        public async Task LateResponse_AfterTheRequestTimedOut_DoesNotDisturbTheConnection()
        {
            // A response that arrives after its request gave up (the default timeout is short) used to be able to
            // throw inside the receive loop and take the whole connection down with it.
            await using var server = new FakeObsWebSocketServer();
            using var client = await ConnectAsync(server);
            client.RequestTimeout = 300;

            var timedOut = client.GetSceneList();
            var (requestType, requestId, _) = await server.ReceiveRequestAsync(Timeout);
            var finished = await Task.WhenAny(timedOut, Task.Delay(Timeout));
            Assert.Same(timedOut, finished);
            await Assert.ThrowsAsync<TimeoutException>(() => timedOut);

            await server.SendRequestSuccessAsync(requestType, requestId, new { scenes = Array.Empty<object>() });

            client.RequestTimeout = 5000;
            var next = client.GetSceneList();
            var (nextType, nextId, _) = await server.ReceiveRequestAsync(Timeout);
            await server.SendRequestSuccessAsync(nextType, nextId, new { scenes = Array.Empty<object>() });

            var response = await next.WithTimeoutAsync(Timeout, "The connection stopped working after a late response.");
            Assert.Empty(response.Scenes);
            Assert.Equal(Enums.ConnectionState.Connected, client.ConnectionState);
        }

        [Fact]
        public async Task PendingRequest_WhenTheConnectionIsLost_FailsImmediatelyInsteadOfTimingOut()
        {
            await using var server = new FakeObsWebSocketServer();
            using var client = await ConnectAsync(server);
            client.RequestTimeout = 60000;

            var pending = client.GetSceneList();
            await server.ReceiveRequestAsync(Timeout);
            server.AbortConnection();

            var finished = await Task.WhenAny(pending, Task.Delay(Timeout));
            Assert.True(ReferenceEquals(pending, finished), "The pending request was still waiting for its timeout after the connection was lost.");
            await Assert.ThrowsAsync<ObsClientException>(() => pending);
        }

        [Fact]
        public async Task UnreadableResponse_FailsOnlyThatRequest_AndTheConnectionSurvives()
        {
            await using var server = new FakeObsWebSocketServer();
            using var client = await ConnectAsync(server);

            var broken = client.GetSceneList();
            var (requestType, requestId, _) = await server.ReceiveRequestAsync(Timeout);
            await server.SendRequestSuccessAsync(requestType, requestId, new { scenes = "this is not a list of scenes" });

            var finished = await Task.WhenAny(broken, Task.Delay(Timeout));
            Assert.Same(broken, finished);
            await Assert.ThrowsAsync<ObsClientException>(() => broken);

            var next = client.GetSceneList();
            var (nextType, nextId, _) = await server.ReceiveRequestAsync(Timeout);
            await server.SendRequestSuccessAsync(nextType, nextId, new { scenes = Array.Empty<object>() });
            await next.WithTimeoutAsync(Timeout, "The connection stopped working after an unreadable response.");

            Assert.Equal(Enums.ConnectionState.Connected, client.ConnectionState);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public async Task Response_WithMultiByteCharactersAcrossTheReceiveBufferBoundary_IsDecodedCorrectly(int padding)
        {
            // The client reads in chunks of 4096 bytes; a UTF-8 character that straddles two chunks used to be
            // decoded chunk by chunk, which corrupted it. The padding moves the boundary through the 3-byte characters.
            await using var server = new FakeObsWebSocketServer();
            using var client = await ConnectAsync(server);

            var text = new string('\u20AC', 3000);
            var request = client.GetInputSettings("some input");
            var (requestType, requestId, _) = await server.ReceiveRequestAsync(Timeout);

            // Sent as raw text, because the serializer would escape the characters instead of sending them as UTF-8.
            var json = "{" + new string(' ', padding) + "\"op\":7,\"d\":{\"requestType\":\"" + requestType + "\",\"requestId\":\"" + requestId +
                "\",\"requestStatus\":{\"result\":true,\"code\":100},\"responseData\":{\"inputKind\":\"text_ft2_source_v2\",\"inputSettings\":{\"text\":\"" + text + "\"}}}}";
            await server.SendRawAsync(json);

            var response = await request.WithTimeoutAsync(Timeout);
            var received = ((JsonElement)response.InputSettings["text"]).GetString();
            Assert.Equal(text, received);
        }

        [Fact]
        public async Task ConcurrentRequests_AreEachAnsweredToTheirOwnCaller()
        {
            await using var server = new FakeObsWebSocketServer();
            using var client = await ConnectAsync(server);

            const int count = 20;
            var calls = Enumerable.Range(0, count).Select(index => client.GetInputSettings($"input-{index}")).ToList();

            var received = new List<(string RequestType, string RequestId, JsonElement? RequestData)>();
            for (var index = 0; index < count; index++)
            {
                received.Add(await server.ReceiveRequestAsync(Timeout));
            }

            // Answered in reverse order, each with the name of the input that was asked about.
            for (var index = received.Count - 1; index >= 0; index--)
            {
                var (type, id, data) = received[index];
                var inputName = data!.Value.GetProperty("inputName").GetString();
                await server.SendRequestSuccessAsync(type, id, new { inputKind = inputName, inputSettings = new { } });
            }

            var responses = await Task.WhenAll(calls).WithTimeoutAsync(Timeout);
            for (var index = 0; index < count; index++)
            {
                Assert.Equal($"input-{index}", responses[index].InputKind);
            }
        }
    }
}
