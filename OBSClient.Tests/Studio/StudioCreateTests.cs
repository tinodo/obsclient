namespace OBSStudioClient.Tests.Studio
{
    using System.Text.Json;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Studio;
    using OBSStudioClient.Tests.TestSupport;

    /// <summary>
    /// The changes that make scenes, inputs and scene items by name, against a stand-in for OBS Studio that answers what a test tells it to: what the requests say, what the
    /// model shows when the call returns, and what the exceptions say when OBS Studio refuses a name. (With the simulation of OBS Studio: <see cref="StudioMakeOverSimulationTests"/>.)
    /// </summary>
    public class StudioCreateTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        [Fact]
        public async Task AScene_IsReturnedWhenTheModelShowsIt()
        {
            await using var test = await ByNameFixture.StartAsync();
            string? asked = null;
            test.Responder.OnRequest = (type, data) =>
            {
                if (type != "CreateScene")
                {
                    return null;
                }

                asked = data?.GetRawText();
                return (100, new { sceneUuid = test.MakeScene("Intro") });
            };

            var scene = await test.Studio.CreateSceneAsync("Intro").WithTimeoutAsync(Timeout);

            Assert.Same(scene, test.Studio.Scenes["Intro"]);
            Assert.Equal(ObsObjectState.Live, scene.State);
            Assert.Empty(scene.Items);
            Assert.Equal("Intro", JsonDocument.Parse(asked!).RootElement.GetProperty("sceneName").GetString());
            Assert.Equal(1, test.Count("CreateScene"));
        }

        [Fact]
        public async Task AScene_WhoseNameIsTakenByAScene_IsANameInUseException_ThatNamesTheScene()
        {
            await using var test = await ByNameFixture.StartAsync();
            test.Responder.OnRequest = (type, data) => type == "CreateScene" ? (601, null) : null;

            var exception = await Assert.ThrowsAsync<ObsNameInUseException>(() => test.Studio.CreateSceneAsync("Gaming").WithTimeoutAsync(Timeout));

            Assert.Equal(RequestStatusCode.ResourceAlreadyExists, exception.ErrorCode);
            Assert.Equal("Gaming", exception.Name);
            Assert.Same(test.Studio.Scenes["Gaming"], exception.HeldBy);
            Assert.Equal("OBS Studio refused creating scene 'Gaming': a scene named 'Gaming' exists already. Nothing was changed.", exception.Message);
            Assert.Equal(1, test.Count("CreateScene"));
            Assert.Equal(4, test.Studio.Scenes.Count);
        }

        [Fact]
        public async Task AScene_WhoseNameIsTakenByAnInput_IsANameInUseException_ThatNamesTheInput()
        {
            await using var test = await ByNameFixture.StartAsync();

            // OBS Studio before obs-websocket 5.7 does not let a scene have the name of an input.
            test.Responder.OnRequest = (type, data) => type == "CreateScene" ? (601, null) : null;

            var exception = await Assert.ThrowsAsync<ObsNameInUseException>(() => test.Studio.CreateSceneAsync("Camera").WithTimeoutAsync(Timeout));

            Assert.Same(test.Studio.Inputs["Camera"], exception.HeldBy);
            Assert.Contains("an input named 'Camera' exists already, and this OBS Studio does not let a scene have the name of an input", exception.Message);
        }

        [Fact]
        public async Task AScene_WhoseNameIsTakenByAnInputThatWasRemovedAndIsAlive_IsANameInUseException_ThatNamesTheScenesThatKeepItAlive()
        {
            await using var test = await ByNameFixture.StartAsync();
            await test.DetachAsync("Overlay", test.Overlay);
            test.Responder.OnRequest = (type, data) => type == "CreateScene" ? (601, null) : null;

            var exception = await Assert.ThrowsAsync<ObsNameInUseException>(() => test.Studio.CreateSceneAsync("Overlay").WithTimeoutAsync(Timeout));

            var overlay = test.FindDetached(test.Overlay);
            Assert.Same(overlay, exception.HeldBy);
            Assert.Equal(ObsObjectState.Detached, overlay.State);
            Assert.Contains("an input named 'Overlay' was removed, but OBS Studio keeps it alive because 2 scene items still show it (", exception.Message);
            Assert.Contains("scene item 3 of scene 'Gaming'", exception.Message);
            Assert.Contains("scene item 2 of scene 'Chatting'", exception.Message);
            Assert.Contains("Call RemoveAsync() on the input (HeldBy of this exception)", exception.Message);
        }

        [Fact]
        public async Task AScene_WhoseNameIsTakenBySomethingThatTheModelDoesNotShow_IsANameInUseException_WithoutAHolder()
        {
            await using var test = await ByNameFixture.StartAsync();
            test.Responder.OnRequest = (type, data) => type == "CreateScene" ? (601, null) : null;

            var exception = await Assert.ThrowsAsync<ObsNameInUseException>(() => test.Studio.CreateSceneAsync("Ghost").WithTimeoutAsync(Timeout));

            Assert.Null(exception.HeldBy);
            Assert.Contains("a source named 'Ghost' exists already, and the model does not show which", exception.Message);
        }

        [Fact]
        public async Task AScene_ThatTheModelDoesNotShowWhenTheCallEnds_IsANotConfirmedException_AndSaysWhy()
        {
            await using var test = await ByNameFixture.StartAsync();

            // OBS Studio says that it made the scene, and what it holds when the model is read does not have it (it was removed again, or OBS Studio changed its collection).
            test.Responder.OnRequest = (type, data) => type == "CreateScene" ? (100, new { sceneUuid = Guid.NewGuid() }) : null;

            var exception = await Assert.ThrowsAsync<ObsNotConfirmedException>(() => test.Studio.CreateSceneAsync("Intro").WithTimeoutAsync(Timeout));

            Assert.Contains("creating scene 'Intro'", exception.Message);
            Assert.Contains("the scene is not in the model", exception.Message);
            Assert.Equal(1, test.Count("CreateScene"));
        }

        [Fact]
        public async Task AScene_OBSStudioCanNotMake_IsAResponseException_WithItsCode()
        {
            await using var test = await ByNameFixture.StartAsync();
            test.Responder.OnRequest = (type, data) => type == "CreateScene" ? (700, null) : null;

            var exception = await Assert.ThrowsAsync<ObsResponseException>(() => test.Studio.CreateSceneAsync("Intro").WithTimeoutAsync(Timeout));

            Assert.Equal(RequestStatusCode.ResourceCreationFailed, exception.ErrorCode);
        }

        [Fact]
        public async Task AScene_WithoutAName_IsRefusedAtOnce()
        {
            await using var test = await ByNameFixture.StartAsync();

            Assert.Throws<ArgumentException>(() => { _ = test.Studio.CreateSceneAsync(string.Empty); });
            Assert.Throws<ArgumentNullException>(() => { _ = test.Studio.CreateSceneAsync(null!); });
            Assert.Equal(0, test.Count("CreateScene"));
        }

        [Fact]
        public async Task AScene_IsNotAskedFor_WithoutAConnection()
        {
            await using var rig = StudioRig.Create(world => world.AddScene("Gaming"));

            await Assert.ThrowsAsync<ObsNotConnectedException>(() => rig.Studio.CreateSceneAsync("Intro").WithTimeoutAsync(Timeout));

            Assert.Equal(0, rig.Responder.RequestLog.Count(logged => logged == "CreateScene"));
        }

        [Fact]
        public async Task AnInput_IsReturnedWithItsSceneItem_AndTheRequestSaysWhatWasAskedFor()
        {
            await using var test = await ByNameFixture.StartAsync();
            var chatting = test.Studio.Scenes["Chatting"];
            string? asked = null;
            test.Responder.OnRequest = (type, data) =>
            {
                if (type != "CreateInput")
                {
                    return null;
                }

                asked = data?.GetRawText();
                var (input, item) = test.MakeInput(test.Chatting, "Logo", "image_source");
                return (100, new { inputUuid = input, sceneItemId = item });
            };

            var logo = await chatting.CreateInputAsync("Logo", "image_source").WithTimeoutAsync(Timeout);

            Assert.Same(logo, test.Studio.Inputs["Logo"]);
            Assert.Equal("image_source", logo.Kind);
            Assert.Equal(ObsObjectState.Live, logo.State);
            Assert.Same(logo, chatting.Items["Logo"].Source);
            Assert.Single(logo.GetSceneItems());
            var request = JsonDocument.Parse(asked!).RootElement;
            Assert.Equal(test.Chatting, request.GetProperty("sceneUuid").GetGuid());
            Assert.Equal("Logo", request.GetProperty("inputName").GetString());
            Assert.Equal("image_source", request.GetProperty("inputKind").GetString());
            Assert.True(request.GetProperty("sceneItemEnabled").GetBoolean());
            Assert.False(request.TryGetProperty("inputSettings", out _));
        }

        [Fact]
        public async Task AnInput_WithSettings_IsAskedForWithThem_AndHiddenWhenTheCallerSaysSo()
        {
            await using var test = await ByNameFixture.StartAsync();
            string? asked = null;
            test.Responder.OnRequest = (type, data) =>
            {
                if (type != "CreateInput")
                {
                    return null;
                }

                asked = data?.GetRawText();
                var (input, item) = test.MakeInput(test.Gaming, "Backdrop");
                return (100, new { inputUuid = input, sceneItemId = item });
            };

            await test.Studio.Scenes["Gaming"].CreateInputAsync("Backdrop", "color_source_v3", new { width = 640, height = 360, color = 4278190335L }, enabled: false).WithTimeoutAsync(Timeout);

            var request = JsonDocument.Parse(asked!).RootElement;
            Assert.False(request.GetProperty("sceneItemEnabled").GetBoolean());
            var settings = request.GetProperty("inputSettings");
            Assert.Equal(640, settings.GetProperty("width").GetInt32());
            Assert.Equal(360, settings.GetProperty("height").GetInt32());
            Assert.Equal(4278190335L, settings.GetProperty("color").GetInt64());
        }

        [Fact]
        public async Task AnInput_WithSettingsAsAJsonElement_OrADictionary_IsAskedForWithThem()
        {
            await using var test = await ByNameFixture.StartAsync();
            var asked = new List<string>();
            test.Responder.OnRequest = (type, data) =>
            {
                if (type != "CreateInput")
                {
                    return null;
                }

                asked.Add(data!.Value.GetRawText());
                var (input, item) = test.MakeInput(test.Gaming, $"Backdrop {asked.Count}");
                return (100, new { inputUuid = input, sceneItemId = item });
            };
            var gaming = test.Studio.Scenes["Gaming"];

            await gaming.CreateInputAsync("Backdrop 1", "color_source_v3", JsonDocument.Parse("{\"width\":10}").RootElement).WithTimeoutAsync(Timeout);
            await gaming.CreateInputAsync("Backdrop 2", "color_source_v3", new Dictionary<string, object> { ["width"] = 20 }).WithTimeoutAsync(Timeout);

            Assert.Equal(10, JsonDocument.Parse(asked[0]).RootElement.GetProperty("inputSettings").GetProperty("width").GetInt32());
            Assert.Equal(20, JsonDocument.Parse(asked[1]).RootElement.GetProperty("inputSettings").GetProperty("width").GetInt32());
        }

        [Fact]
        public async Task AnInputWithAudio_IsCompleteWhenTheCallReturns()
        {
            await using var test = await ByNameFixture.StartAsync();
            test.Responder.OnRequest = (type, data) =>
            {
                if (type != "CreateInput")
                {
                    return null;
                }

                var (input, item) = test.MakeInput(test.Gaming, "Headset", "wasapi_input_capture", 2, muted: true, volumeMul: 0.5);
                return (100, new { inputUuid = input, sceneItemId = item });
            };

            var headset = await test.Studio.Scenes["Gaming"].CreateInputAsync("Headset", "wasapi_input_capture").WithTimeoutAsync(Timeout);

            // The model holds what OBS Studio holds for the input when the call returns, not only when it has caught up later.
            Assert.True(headset.HasAudio);
            Assert.True(headset.IsMuted);
            Assert.Equal(0.5f, headset.VolumeMul);
        }

        [Theory]
        [InlineData(5)]
        [InlineData("text")]
        [InlineData(true)]
        public async Task AnInput_WithSettingsThatAreNotAnObject_IsRefusedAtOnce(object settings)
        {
            await using var test = await ByNameFixture.StartAsync();
            var gaming = test.Studio.Scenes["Gaming"];

            var exception = Assert.Throws<ArgumentException>(() => { _ = gaming.CreateInputAsync("Backdrop", "color_source_v3", settings); });

            Assert.Equal("settings", exception.ParamName);
            Assert.Equal(0, test.Count("CreateInput"));
        }

        [Fact]
        public async Task AnInput_WithSettingsThatAreAnArray_OrWithoutANameOrAKind_IsRefusedAtOnce()
        {
            await using var test = await ByNameFixture.StartAsync();
            var gaming = test.Studio.Scenes["Gaming"];

            Assert.Throws<ArgumentException>(() => { _ = gaming.CreateInputAsync("Backdrop", "color_source_v3", new[] { 1, 2 }); });
            Assert.Throws<ArgumentException>(() => { _ = gaming.CreateInputAsync(string.Empty, "color_source_v3"); });
            Assert.Throws<ArgumentException>(() => { _ = gaming.CreateInputAsync("Backdrop", string.Empty); });
            Assert.Equal(0, test.Count("CreateInput"));
        }

        [Fact]
        public async Task AnInput_WhoseNameIsTakenByAnInput_IsANameInUseException_ThatNamesTheInput()
        {
            await using var test = await ByNameFixture.StartAsync();
            test.Responder.OnRequest = (type, data) => type == "CreateInput" ? (601, null) : null;

            var exception = await Assert.ThrowsAsync<ObsNameInUseException>(() => test.Studio.Scenes["Gaming"].CreateInputAsync("Camera", "color_source_v3").WithTimeoutAsync(Timeout));

            Assert.Same(test.Studio.Inputs["Camera"], exception.HeldBy);
            Assert.Equal("OBS Studio refused creating input 'Camera' in scene 'Gaming': an input named 'Camera' exists already. Nothing was changed.", exception.Message);
        }

        [Fact]
        public async Task AnInput_WhoseNameIsTakenByAScene_IsANameInUseException_ThatSaysThatAnInputCanNotHaveTheNameOfAScene()
        {
            await using var test = await ByNameFixture.StartAsync();
            test.Responder.OnRequest = (type, data) => type == "CreateInput" ? (601, null) : null;

            var exception = await Assert.ThrowsAsync<ObsNameInUseException>(() => test.Studio.Scenes["Gaming"].CreateInputAsync("Chatting", "color_source_v3").WithTimeoutAsync(Timeout));

            Assert.Same(test.Studio.Scenes["Chatting"], exception.HeldBy);
            Assert.Contains("a scene named 'Chatting' exists already, and an input can not have the name of a scene", exception.Message);
        }

        [Fact]
        public async Task AnInput_WhoseNameIsTakenByAnInputThatWasRemovedAndIsAlive_IsANameInUseException_ThatSaysWhatKeepsItAlive()
        {
            await using var test = await ByNameFixture.StartAsync();
            await test.DetachAsync("Overlay", test.Overlay);
            test.Responder.OnRequest = (type, data) => type == "CreateInput" ? (601, null) : null;

            var exception = await Assert.ThrowsAsync<ObsNameInUseException>(() => test.Studio.Scenes["Gaming"].CreateInputAsync("Overlay", "color_source_v3").WithTimeoutAsync(Timeout));

            var overlay = test.FindDetached(test.Overlay);
            Assert.Same(overlay, exception.HeldBy);
            Assert.False(test.Studio.Inputs.Contains("Overlay"));
            Assert.Contains("an input named 'Overlay' was removed, but OBS Studio keeps it alive because 2 scene items still show it", exception.Message);
            Assert.Contains("and a name stays taken until the last of them is gone", exception.Message);
        }

        [Fact]
        public async Task AnInput_WhoseNameIsTakenByAnInputThatWasRemovedAndIsAliveForOneSceneItem_SaysItInTheSingular()
        {
            await using var test = await ByNameFixture.StartAsync();
            test.RemoveItem(test.Chatting, 2);
            await test.Responder.SendEventAsync("SceneItemRemoved", new { sceneName = "Chatting", sceneUuid = test.Chatting, sourceName = "Overlay", sourceUuid = test.Overlay, sceneItemId = 2 }, 128);
            await StudioRig.EventuallyAsync(() => test.Studio.Scenes["Chatting"].Items.Count == 1, "the scene item to be removed");
            await test.DetachAsync("Overlay", test.Overlay);
            test.Responder.OnRequest = (type, data) => type == "CreateInput" ? (601, null) : null;

            var exception = await Assert.ThrowsAsync<ObsNameInUseException>(() => test.Studio.Scenes["Gaming"].CreateInputAsync("Overlay", "color_source_v3").WithTimeoutAsync(Timeout));

            Assert.Contains("because 1 scene item still shows it (scene item 3 of scene 'Gaming')", exception.Message);
        }

        [Fact]
        public async Task AnInput_InASceneThatWasRemoved_IsNotAskedFor()
        {
            await using var test = await ByNameFixture.StartAsync();
            var chatting = test.Studio.Scenes["Chatting"];
            test.RemoveScene(test.Chatting);
            await StudioRig.EventuallyAsync(() => chatting.IsRemoved, "the scene to be removed");

            // The scene is shown by a scene item of another scene, so OBS Studio keeps it alive and the model still has it: it can not be changed.
            var exception = await Assert.ThrowsAsync<ObsRemovedException>(() => chatting.CreateInputAsync("Logo", "image_source").WithTimeoutAsync(Timeout));

            Assert.Equal(ObsObjectState.Detached, exception.State);
            Assert.Equal("scene 'Chatting'", exception.Description);
            Assert.Equal(0, test.Count("CreateInput"));
        }

        [Fact]
        public async Task AnInput_ThatTheModelDoesNotShowWhenTheCallEnds_IsANotConfirmedException()
        {
            await using var test = await ByNameFixture.StartAsync();

            // OBS Studio says that it made the input, and what it holds when the model is read does not have it (it was removed again, or OBS Studio changed its collection).
            test.Responder.OnRequest = (type, data) => type == "CreateInput" ? (100, new { inputUuid = Guid.NewGuid(), sceneItemId = 9 }) : null;

            var exception = await Assert.ThrowsAsync<ObsNotConfirmedException>(() => test.Studio.Scenes["Gaming"].CreateInputAsync("Logo", "color_source_v3").WithTimeoutAsync(Timeout));

            Assert.Contains("creating input 'Logo' in scene 'Gaming'", exception.Message);
            Assert.Contains("the input is not in the model", exception.Message);
        }

        [Fact]
        public async Task AnInput_WhoseSceneItemTheModelDoesNotShow_IsReturnedAllTheSame()
        {
            await using var test = await ByNameFixture.StartAsync();

            // The input is made, and the scene item that the answer names is not there when the model is read: the input is what the call made, and it is alive.
            test.Responder.OnRequest = (type, data) =>
            {
                if (type != "CreateInput")
                {
                    return null;
                }

                var (input, _) = test.MakeInput(test.Chatting, "Logo");
                return (100, new { inputUuid = input, sceneItemId = 99 });
            };

            var logo = await test.Studio.Scenes["Chatting"].CreateInputAsync("Logo", "color_source_v3").WithTimeoutAsync(Timeout);

            Assert.Same(logo, test.Studio.Inputs["Logo"]);
            var items = logo.GetSceneItems();
            Assert.Single(items);
            Assert.NotEqual(99, items[0].Id);
        }

        [Fact]
        public async Task AnInput_OfAKindThatOBSStudioDoesNotKnow_IsAResponseException_WithItsCode()
        {
            await using var test = await ByNameFixture.StartAsync();
            test.Responder.OnRequest = (type, data) => type == "CreateInput" ? (605, null) : null;

            var exception = await Assert.ThrowsAsync<ObsResponseException>(() => test.Studio.Scenes["Gaming"].CreateInputAsync("Logo", "no_such_kind").WithTimeoutAsync(Timeout));

            Assert.Equal(RequestStatusCode.InvalidInputKind, exception.ErrorCode);
        }

        [Fact]
        public async Task ASceneItem_IsReturnedWhenTheModelShowsIt_AndTheRequestSaysWhatWasAskedFor()
        {
            await using var test = await ByNameFixture.StartAsync();
            var scene = test.Studio.Scenes["Scene"];
            var camera = test.Studio.Inputs["Camera"];
            var sceneUuid = scene.Uuid;
            string? asked = null;
            test.Responder.OnRequest = (type, data) =>
            {
                if (type != "CreateSceneItem")
                {
                    return null;
                }

                asked = data?.GetRawText();
                return (100, new { sceneItemId = test.MakeItem(sceneUuid, test.Camera, "Camera") });
            };

            var item = await scene.AddAsync(camera, enabled: false).WithTimeoutAsync(Timeout);

            Assert.Same(item, scene.Items["Camera"]);
            Assert.Same(camera, item.Source);
            Assert.Equal(1, item.Id);
            var request = JsonDocument.Parse(asked!).RootElement;
            Assert.Equal(scene.Uuid, request.GetProperty("sceneUuid").GetGuid());
            Assert.Equal(test.Camera, request.GetProperty("sourceUuid").GetGuid());
            Assert.False(request.GetProperty("sceneItemEnabled").GetBoolean());
        }

        [Fact]
        public async Task AScene_CanBeShownByASceneItem_AndTheSameSourceMoreThanOnce()
        {
            await using var test = await ByNameFixture.StartAsync();
            var scene = test.Studio.Scenes["Scene"];
            var chatting = test.Studio.Scenes["Chatting"];
            test.Responder.OnRequest = (type, data) =>
            {
                if (type != "CreateSceneItem")
                {
                    return null;
                }

                var source = data!.Value.GetProperty("sourceUuid").GetGuid();
                return (100, new { sceneItemId = test.MakeItem(scene.Uuid, source, source == test.Chatting ? "Chatting" : "Camera", isScene: source == test.Chatting) });
            };

            var first = await scene.AddAsync(chatting).WithTimeoutAsync(Timeout);
            var second = await scene.AddAsync(test.Studio.Inputs["Camera"]).WithTimeoutAsync(Timeout);
            var third = await scene.AddAsync(test.Studio.Inputs["Camera"]).WithTimeoutAsync(Timeout);

            Assert.Same(chatting, first.Source);
            Assert.Equal(new[] { 1, 2, 3 }, new[] { first.Id, second.Id, third.Id });
            Assert.Equal(3, scene.Items.Count);
            Assert.Equal(2, scene.Items.FindAll("Camera").Count);
        }

        [Fact]
        public async Task ASceneItem_ThatTheModelDoesNotShowWhenTheCallEnds_IsANotConfirmedException()
        {
            await using var test = await ByNameFixture.StartAsync();
            test.Responder.OnRequest = (type, data) => type == "CreateSceneItem" ? (100, new { sceneItemId = 77 }) : null;

            var exception = await Assert.ThrowsAsync<ObsNotConfirmedException>(() => test.Studio.Scenes["Scene"].AddAsync(test.Studio.Inputs["Camera"]).WithTimeoutAsync(Timeout));

            Assert.Contains("adding input 'Camera' to scene 'Scene'", exception.Message);
            Assert.Contains("the scene item is not in the model", exception.Message);
        }

        [Fact]
        public async Task ASceneItem_ThatWouldMakeASceneContainItself_IsAResponseException_WithItsCode()
        {
            await using var test = await ByNameFixture.StartAsync();
            test.Responder.OnRequest = (type, data) => type == "CreateSceneItem" ? (703, null) : null;

            var exception = await Assert.ThrowsAsync<ObsResponseException>(() => test.Studio.Scenes["Chatting"].AddAsync(test.Studio.Scenes["Gaming"]).WithTimeoutAsync(Timeout));

            Assert.Equal(RequestStatusCode.CannotAct, exception.ErrorCode);
            Assert.Equal(1, test.Count("CreateSceneItem"));
        }

        [Fact]
        public async Task ASceneItem_IsRefusedAtOnce_ForASourceThatIsNotAllowed()
        {
            await using var test = await ByNameFixture.StartAsync();
            var gaming = test.Studio.Scenes["Gaming"];
            await using var other = await ByNameFixture.StartAsync();

            Assert.Throws<ArgumentNullException>(() => { _ = gaming.AddAsync(null!); });
            Assert.Throws<ArgumentException>(() => { _ = gaming.AddAsync(gaming); });
            var foreign = Assert.Throws<ArgumentException>(() => { _ = gaming.AddAsync(other.Studio.Inputs["Camera"]); });
            Assert.Equal("source", foreign.ParamName);
            Assert.Equal(0, test.Count("CreateSceneItem"));
        }

        [Fact]
        public async Task ASceneItem_ForASourceThatWasRemoved_IsNotAskedFor()
        {
            await using var test = await ByNameFixture.StartAsync();
            await test.DetachAsync("Overlay", test.Overlay);
            var overlay = test.FindDetached(test.Overlay);

            var exception = await Assert.ThrowsAsync<ObsRemovedException>(() => test.Studio.Scenes["Scene"].AddAsync(overlay).WithTimeoutAsync(Timeout));

            Assert.Equal(ObsObjectState.Detached, exception.State);
            Assert.Equal("input 'Overlay'", exception.Description);
            Assert.Equal(0, test.Count("CreateSceneItem"));
        }

        [Fact]
        public async Task ASceneItem_InASceneThatWasRemoved_IsNotAskedFor()
        {
            await using var test = await ByNameFixture.StartAsync();
            var chatting = test.Studio.Scenes["Chatting"];
            test.RemoveScene(test.Chatting);
            await StudioRig.EventuallyAsync(() => chatting.IsRemoved, "the scene to be removed");

            var exception = await Assert.ThrowsAsync<ObsRemovedException>(() => chatting.AddAsync(test.Studio.Inputs["Camera"]).WithTimeoutAsync(Timeout));

            Assert.Equal("scene 'Chatting'", exception.Description);
            Assert.Equal(0, test.Count("CreateSceneItem"));
        }
    }
}
