namespace OBSStudioClient.Tests.Unit
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Messages;
    using OBSStudioClient.Tests.TestSupport;
    using System.Text.Json;

    /// <summary>
    /// obs-websocket applies only the members of a transform that are present (RequestHandler_SceneItems.cpp,
    /// SetSceneItemTransform), refuses a bounds width or height below 1, and refuses an update without any member.
    /// </summary>
    public class SceneItemTransformUpdateTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private static readonly string[] ReadOnlyMembers = ["width", "height", "sourceWidth", "sourceHeight"];

        // What GetSceneItemTransform returns for an item without bounds.
        private static SceneItemTransform TransformWithoutBounds() =>
            new(5, 0, 0f, Bounds.OBS_BOUNDS_NONE, 0f, 1, 2, 3, 4, 100f, 10f, 20f, 30f, 1.5f, 2.5f, 100f, 100f, 100f) { CropToBounds = false };

        private static SceneItemTransform TransformWithBounds() =>
            new(5, 0, 120f, Bounds.OBS_BOUNDS_STRETCH, 200f, 0, 0, 0, 0, 100f, 10f, 20f, 0f, 1f, 1f, 100f, 100f, 100f) { CropToBounds = true };

        private static async Task<ObsClient> ConnectAsync(FakeObsWebSocketServer server)
        {
            server.Start();
            var client = new ObsClient { RequestTimeout = 5000 };
            var connectTask = client.ConnectAsync(hostname: "127.0.0.1", port: server.Port);
            await server.HandshakeAsync().WithTimeoutAsync(Timeout);
            await connectTask.WithTimeoutAsync(Timeout);
            return client;
        }

        private static string[] MemberNames(JsonElement element) => element.EnumerateObject().Select(member => member.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray();

        [Fact]
        public void OnlyTheMembersThatAreSet_AreSerialized()
        {
            var json = JsonSerializer.Serialize(new SceneItemTransformUpdate { PositionX = 12.5f, BoundsType = Bounds.OBS_BOUNDS_STRETCH, CropToBounds = false });

            using var document = JsonDocument.Parse(json);
            Assert.Equal(new[] { "boundsType", "cropToBounds", "positionX" }, MemberNames(document.RootElement));
            Assert.Equal("OBS_BOUNDS_STRETCH", document.RootElement.GetProperty("boundsType").GetString());
            Assert.Equal(12.5f, document.RootElement.GetProperty("positionX").GetSingle());
            Assert.False(document.RootElement.GetProperty("cropToBounds").GetBoolean());
        }

        [Fact]
        public void AnUpdateWithoutMembers_IsEmpty_AndSerializesToAnEmptyObject()
        {
            var update = new SceneItemTransformUpdate();

            Assert.True(update.IsEmpty);
            Assert.Equal("{}", JsonSerializer.Serialize(update));
            Assert.False((update with { Rotation = 0f }).IsEmpty);
        }

        [Fact]
        public void ATransformWithoutBounds_BecomesAnUpdateWithoutTheBounds_ThatObsStudioWouldRefuse()
        {
            var update = TransformWithoutBounds().ToUpdate();

            Assert.Null(update.BoundsWidth);
            Assert.Null(update.BoundsHeight);
            Assert.Equal(Bounds.OBS_BOUNDS_NONE, update.BoundsType);
            Assert.Equal(10f, update.PositionX);
            Assert.Equal(20f, update.PositionY);
            Assert.Equal(30f, update.Rotation);
            Assert.Equal(1.5f, update.ScaleX);
            Assert.Equal(2.5f, update.ScaleY);
            Assert.Equal(5, update.Alignment);
            Assert.Equal(1, update.CropBottom);
            Assert.Equal(2, update.CropLeft);
            Assert.Equal(3, update.CropRight);
            Assert.Equal(4, update.CropTop);
            Assert.False(update.CropToBounds);
        }

        [Fact]
        public void ATransformWithBounds_KeepsItsBounds()
        {
            var update = TransformWithBounds().ToUpdate();

            Assert.Equal(200f, update.BoundsWidth);
            Assert.Equal(120f, update.BoundsHeight);
            Assert.Equal(Bounds.OBS_BOUNDS_STRETCH, update.BoundsType);
            Assert.True(update.CropToBounds);
        }

        [Fact]
        public void TheMembersThatObsStudioCalculates_AreNotPartOfAnUpdate()
        {
            using var document = JsonDocument.Parse(JsonSerializer.Serialize(TransformWithBounds().ToUpdate()));

            Assert.Empty(MemberNames(document.RootElement).Intersect(ReadOnlyMembers));
        }

        [Fact]
        public void ATransform_ConvertsToAnUpdateImplicitly()
        {
            var transform = TransformWithBounds();

            SceneItemTransformUpdate update = transform;

            Assert.Equal(transform.ToUpdate(), update);
        }

        [Fact]
        public void AWithExpression_ChangesOnlyTheNamedMembers()
        {
            var current = TransformWithBounds().ToUpdate();

            var changed = current with { ScaleX = 3f };

            Assert.Equal(3f, changed.ScaleX);
            Assert.Equal(current with { ScaleX = 3f }, changed);
            Assert.Equal(current.PositionX, changed.PositionX);
            Assert.NotEqual(current, changed);
        }

        [Fact]
        public async Task SetSceneItemTransform_WithAnUpdate_SendsOnlyTheMembersThatAreSet()
        {
            await using var server = new FakeObsWebSocketServer();
            using var client = await ConnectAsync(server);

            var set = client.SetSceneItemTransform("Scene", 7, new SceneItemTransformUpdate { PositionX = 100f });
            var (requestType, requestId, data) = await server.ReceiveRequestAsync(Timeout);
            await server.SendRequestSuccessAsync(requestType, requestId);
            await set.WithTimeoutAsync(Timeout);

            Assert.Equal("SetSceneItemTransform", requestType);
            var transform = data!.Value.GetProperty("sceneItemTransform");
            Assert.Equal(new[] { "positionX" }, MemberNames(transform));
            Assert.Equal(100f, transform.GetProperty("positionX").GetSingle());
        }

        [Fact]
        public async Task SetSceneItemTransform_WithATransformReadFromObs_DoesNotSendWhatObsStudioWouldRefuse()
        {
            // Regression test: the full transform of an item without bounds used to be sent as it was read, including a
            // boundsWidth and boundsHeight of 0, which OBS Studio refuses (they must be at least 1), so such an item
            // could not be given its own transform back.
            await using var server = new FakeObsWebSocketServer();
            using var client = await ConnectAsync(server);

            var set = client.SetSceneItemTransform("Scene", 7, TransformWithoutBounds());
            var (requestType, requestId, data) = await server.ReceiveRequestAsync(Timeout);
            await server.SendRequestSuccessAsync(requestType, requestId);
            await set.WithTimeoutAsync(Timeout);

            var names = MemberNames(data!.Value.GetProperty("sceneItemTransform"));
            Assert.DoesNotContain("boundsWidth", names);
            Assert.DoesNotContain("boundsHeight", names);
            Assert.Empty(names.Intersect(ReadOnlyMembers));
            Assert.Contains("positionX", names);
            Assert.Contains("boundsType", names);
        }

        [Fact]
        public void TheBatchBuilder_PutsOnlyTheMembersThatAreSetInTheRequest()
        {
            var batch = new RequestBatchMessage();
            var sceneUuid = Guid.NewGuid();

            batch.AddSetSceneItemTransformRequest(sceneUuid, 7, new SceneItemTransformUpdate { ScaleX = 2f, ScaleY = 2f });

            var request = Assert.Single(batch.Requests);
            Assert.Equal(RequestType.SetSceneItemTransform, request.RequestType);
            Assert.Equal(sceneUuid, request.RequestData!.Value.GetProperty("sceneUuid").GetGuid());
            Assert.Equal(new[] { "scaleX", "scaleY" }, MemberNames(request.RequestData!.Value.GetProperty("sceneItemTransform")));
        }
    }
}
