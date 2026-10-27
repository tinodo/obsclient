namespace OBSStudioClient.Tests.Unit
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Messages;
    using OBSStudioClient.Tests.TestSupport;
    using System.Text.Json;

    /// <summary>
    /// What the generated request methods and batch builders put on the wire: an object that is identified by name sends the
    /// name field and nothing else, one that is identified by UUID sends the UUID field; what is left out is not sent, except where
    /// obs-websocket gives a null a meaning; and what is wrong is refused before anything is sent.
    /// </summary>
    public class RequestWireFormatTests
    {
        [Fact]
        public async Task AnObjectGivenByName_SendsTheNameField()
        {
            var (sent, muted) = await ObsClientHarness.QueryAsync(client => client.GetInputMute("Microphone"), new { inputMuted = true });

            Assert.Equal("GetInputMute", sent.RequestType);
            Assert.Equal(new[] { "inputName" }, sent.Fields);
            Assert.Equal("Microphone", sent["inputName"].GetString());
            Assert.True(muted);
        }

        [Fact]
        public async Task AnObjectGivenByUuid_SendsTheUuidField()
        {
            var uuid = Guid.NewGuid();

            var (sent, muted) = await ObsClientHarness.QueryAsync(client => client.GetInputMute(uuid), new { inputMuted = false });

            Assert.Equal(new[] { "inputUuid" }, sent.Fields);
            Assert.Equal(uuid, sent["inputUuid"].GetGuid());
            Assert.False(muted);
        }

        [Fact]
        public async Task AnObjectThatObsStudioReported_IsSentByItsUuid()
        {
            var scene = new Scene(0, "Game", Guid.NewGuid());

            var sent = await ObsClientHarness.SendAsync(client => client.SetCurrentProgramScene(scene));

            Assert.Equal(new[] { "sceneUuid" }, sent.Fields);
            Assert.Equal(scene.SceneUuid, sent["sceneUuid"].GetGuid());
        }

        [Fact]
        public async Task TheCanvas_IsSentNextToTheScene()
        {
            var canvasUuid = Guid.NewGuid();

            var (sent, items) = await ObsClientHarness.QueryAsync(client => client.GetSceneItemList("Game", canvasUuid), new { sceneItems = Array.Empty<object>() });

            Assert.Equal(new[] { "canvasUuid", "sceneName" }, sent.Fields);
            Assert.Equal(canvasUuid, sent["canvasUuid"].GetGuid());
            Assert.Empty(items);
        }

        [Fact]
        public async Task AnOptionalObject_LeftOut_IsNotSent()
        {
            var sent = await ObsClientHarness.SendAsync(client => client.DuplicateSceneItem("Game", 3), new { sceneItemId = 4 });

            Assert.Equal(new[] { "sceneItemId", "sceneName" }, sent.Fields);
        }

        [Fact]
        public async Task AnOptionalObject_IsSentByItsOwnFields()
        {
            var destination = Guid.NewGuid();

            var sent = await ObsClientHarness.SendAsync(client => client.DuplicateSceneItem("Game", 3, destination), new { sceneItemId = 4 });

            Assert.Equal(new[] { "destinationSceneUuid", "sceneItemId", "sceneName" }, sent.Fields);
            Assert.Equal(destination, sent["destinationSceneUuid"].GetGuid());
        }

        [Fact]
        public async Task OptionalArguments_LeftOut_AreNotSent()
        {
            var sent = await ObsClientHarness.SendAsync(client => client.SetVideoSettings(baseWidth: 1920));

            Assert.Equal(new[] { "baseWidth" }, sent.Fields);
            Assert.Equal(1920, sent["baseWidth"].GetInt32());
        }

        [Fact]
        public async Task ARequestWithNothingToSend_SendsNoRequestData()
        {
            var sent = await ObsClientHarness.SendAsync(client => client.GetInputKindList(), new { inputKinds = Array.Empty<string>() });

            Assert.Null(sent.Data);
        }

        [Fact]
        public async Task ANullThatRemovesTheValue_IsSentAsNull()
        {
            // obs-websocket removes the transition that is overridden for a scene when the field is null, and leaves it
            // alone when the field is absent (RequestHandler_Scenes.cpp), so the client has to say which one it means.
            var sent = await ObsClientHarness.SendAsync(client => client.SetSceneSceneTransitionOverride("Game", null, 500));

            Assert.Equal(new[] { "sceneName", "transitionDuration", "transitionName" }, sent.Fields);
            Assert.Equal(JsonValueKind.Null, sent["transitionName"].ValueKind);
            Assert.Equal(500, sent["transitionDuration"].GetInt32());
        }

        [Fact]
        public async Task ANullProfileParameter_IsSentAsNull()
        {
            var sent = await ObsClientHarness.SendAsync(client => client.SetProfileParameter("SimpleOutput", "Preset", null));

            Assert.Equal(new[] { "parameterCategory", "parameterName", "parameterValue" }, sent.Fields);
            Assert.Equal(JsonValueKind.Null, sent["parameterValue"].ValueKind);
        }

        [Fact]
        public async Task AnObjectThatIsNotGiven_IsRefusedBeforeAnythingIsSent()
        {
            // The client is not even connected: the arguments are checked first.
            using var client = new ObsClient();

            var refusal = await Assert.ThrowsAsync<ArgumentException>(() => client.GetInputMute(default));

            Assert.Equal("input", refusal.ParamName);
        }

        [Fact]
        public async Task ARequiredArgumentThatIsNull_IsRefusedBeforeAnythingIsSent()
        {
            using var client = new ObsClient();

            var refusal = await Assert.ThrowsAsync<ArgumentNullException>(() => client.SetSourceFilterSettings("Camera", "Blur", null!));

            Assert.Equal("filterSettings", refusal.ParamName);
        }

        [Fact]
        public void TheBatchBuilder_PutsTheSameFieldsInTheRequest()
        {
            var batch = new RequestBatchMessage();
            var uuid = Guid.NewGuid();

            batch.AddSetInputMuteRequest("Microphone", true);
            batch.AddSetInputMuteRequest(uuid, false);

            var requests = batch.Requests;
            Assert.All(requests, request => Assert.Equal(RequestType.SetInputMute, request.RequestType));
            Assert.Equal("Microphone", requests[0].RequestData!.Value.GetProperty("inputName").GetString());
            Assert.False(requests[0].RequestData!.Value.TryGetProperty("inputUuid", out _));
            Assert.Equal(uuid, requests[1].RequestData!.Value.GetProperty("inputUuid").GetGuid());
            Assert.False(requests[1].RequestData!.Value.TryGetProperty("inputName", out _));
        }

        [Fact]
        public void TheBatchBuilder_OfARequestWithNothingToSend_HasNoRequestData()
        {
            var batch = new RequestBatchMessage();

            batch.AddStartRecordRequest();

            var request = Assert.Single(batch.Requests);
            Assert.Equal(RequestType.StartRecord, request.RequestType);
            Assert.Null(request.RequestData);
        }

        [Fact]
        public void TheBatchBuilder_SendsANullThatRemovesTheValue()
        {
            var batch = new RequestBatchMessage();

            batch.AddSetSceneSceneTransitionOverrideRequest("Game", null, null);

            var data = Assert.Single(batch.Requests).RequestData!.Value;
            Assert.Equal(JsonValueKind.Null, data.GetProperty("transitionName").ValueKind);
            Assert.Equal(JsonValueKind.Null, data.GetProperty("transitionDuration").ValueKind);
        }

        [Fact]
        public void TheBatchBuilder_RefusesAnObjectThatIsNotGiven_AndAddsNothing()
        {
            var batch = new RequestBatchMessage();

            var refusal = Assert.Throws<ArgumentException>(() => batch.AddSetInputMuteRequest(default, true));

            Assert.Equal("input", refusal.ParamName);
            Assert.Empty(batch.Requests);
        }
    }
}
