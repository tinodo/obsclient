namespace OBSStudioClient.Tests.Model
{
    using System.Text.Json;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Model;
    using OBSStudioClient.Tests.TestSupport;

    /// <summary>
    /// The changes that make scenes, inputs and scene items by name, against a stand-in for OBS Studio that answers what a test tells it to: what the requests say, what the
    /// model shows when the call returns, and what the exceptions say when OBS Studio refuses a name. (With the simulation of OBS Studio: <see cref="ModelMakeOverSimulationTests"/>.)
    /// </summary>
    public class ModelCreateTests
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

            var scene = await test.Model.CreateSceneAsync("Intro").WithTimeoutAsync(Timeout);

            Assert.Same(scene, test.Model.Scenes["Intro"]);
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

            var exception = await Assert.ThrowsAsync<ObsNameInUseException>(() => test.Model.CreateSceneAsync("Gaming").WithTimeoutAsync(Timeout));

            Assert.Equal(RequestStatusCode.ResourceAlreadyExists, exception.ErrorCode);
            Assert.Equal("Gaming", exception.Name);
            Assert.Same(test.Model.Scenes["Gaming"], exception.HeldBy);
            Assert.Equal("OBS Studio refused creating scene 'Gaming': a scene named 'Gaming' exists already. Nothing was changed.", exception.Message);
            Assert.Equal(1, test.Count("CreateScene"));
            Assert.Equal(4, test.Model.Scenes.Count);
        }

        [Fact]
        public async Task AScene_WhoseNameIsTakenByAnInput_IsANameInUseException_ThatNamesTheInput()
        {
            await using var test = await ByNameFixture.StartAsync();

            // OBS Studio before obs-websocket 5.7 does not let a scene have the name of an input.
            test.Responder.OnRequest = (type, data) => type == "CreateScene" ? (601, null) : null;

            var exception = await Assert.ThrowsAsync<ObsNameInUseException>(() => test.Model.CreateSceneAsync("Camera").WithTimeoutAsync(Timeout));

            Assert.Same(test.Model.Inputs["Camera"], exception.HeldBy);
            Assert.Contains("an input named 'Camera' exists already, and this OBS Studio does not let a scene have the name of an input", exception.Message);
        }

        [Fact]
        public async Task AScene_WhoseNameIsTakenByAnInputThatWasRemovedAndIsAlive_IsANameInUseException_ThatNamesTheScenesThatKeepItAlive()
        {
            await using var test = await ByNameFixture.StartAsync();
            await test.DetachAsync("Overlay", test.Overlay);
            test.Responder.OnRequest = (type, data) => type == "CreateScene" ? (601, null) : null;

            var exception = await Assert.ThrowsAsync<ObsNameInUseException>(() => test.Model.CreateSceneAsync("Overlay").WithTimeoutAsync(Timeout));

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

            var exception = await Assert.ThrowsAsync<ObsNameInUseException>(() => test.Model.CreateSceneAsync("Ghost").WithTimeoutAsync(Timeout));

            Assert.Null(exception.HeldBy);
            Assert.Contains("a source named 'Ghost' exists already, and the model does not show which", exception.Message);
        }

        [Fact]
        public async Task AScene_ThatTheModelDoesNotShowWhenTheCallEnds_IsANotConfirmedException_AndSaysWhy()
        {
            await using var test = await ByNameFixture.StartAsync();

            // OBS Studio says that it made the scene, and what it holds when the model is read does not have it (it was removed again, or OBS Studio changed its collection).
            test.Responder.OnRequest = (type, data) => type == "CreateScene" ? (100, new { sceneUuid = Guid.NewGuid() }) : null;

            var exception = await Assert.ThrowsAsync<ObsNotConfirmedException>(() => test.Model.CreateSceneAsync("Intro").WithTimeoutAsync(Timeout));

            Assert.Contains("creating scene 'Intro'", exception.Message);
            Assert.Contains("the scene is not in the model", exception.Message);
            Assert.Equal(1, test.Count("CreateScene"));
        }

        [Fact]
        public async Task AScene_OBSStudioCanNotMake_IsAResponseException_WithItsCode()
        {
            await using var test = await ByNameFixture.StartAsync();
            test.Responder.OnRequest = (type, data) => type == "CreateScene" ? (700, null) : null;

            var exception = await Assert.ThrowsAsync<ObsResponseException>(() => test.Model.CreateSceneAsync("Intro").WithTimeoutAsync(Timeout));

            Assert.Equal(RequestStatusCode.ResourceCreationFailed, exception.ErrorCode);
        }

        [Fact]
        public async Task AScene_WithoutAName_IsRefusedAtOnce()
        {
            await using var test = await ByNameFixture.StartAsync();

            Assert.Throws<ArgumentException>(() => { _ = test.Model.CreateSceneAsync(string.Empty); });
            Assert.Throws<ArgumentNullException>(() => { _ = test.Model.CreateSceneAsync(null!); });
            Assert.Equal(0, test.Count("CreateScene"));
        }

        [Fact]
        public async Task AScene_IsNotAskedFor_WithoutAConnection()
        {
            await using var rig = ModelRig.Create(world => world.AddScene("Gaming"));

            await Assert.ThrowsAsync<ObsNotConnectedException>(() => rig.Model.CreateSceneAsync("Intro").WithTimeoutAsync(Timeout));

            Assert.Equal(0, rig.Responder.RequestLog.Count(logged => logged == "CreateScene"));
        }

        [Fact]
        public async Task AnInput_IsReturnedWithItsSceneItem_AndTheRequestSaysWhatWasAskedFor()
        {
            await using var test = await ByNameFixture.StartAsync();
            var chatting = test.Model.Scenes["Chatting"];
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

            Assert.Same(logo, test.Model.Inputs["Logo"]);
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

            await test.Model.Scenes["Gaming"].CreateInputAsync("Backdrop", "color_source_v3", new { width = 640, height = 360, color = 4278190335L }, enabled: false).WithTimeoutAsync(Timeout);

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
            var gaming = test.Model.Scenes["Gaming"];

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

            var headset = await test.Model.Scenes["Gaming"].CreateInputAsync("Headset", "wasapi_input_capture").WithTimeoutAsync(Timeout);

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
            var gaming = test.Model.Scenes["Gaming"];

            var exception = Assert.Throws<ArgumentException>(() => { _ = gaming.CreateInputAsync("Backdrop", "color_source_v3", settings); });

            Assert.Equal("settings", exception.ParamName);
            Assert.Equal(0, test.Count("CreateInput"));
        }

        [Fact]
        public async Task AnInput_WithSettingsThatAreAnArray_OrWithoutANameOrAKind_IsRefusedAtOnce()
        {
            await using var test = await ByNameFixture.StartAsync();
            var gaming = test.Model.Scenes["Gaming"];

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

            var exception = await Assert.ThrowsAsync<ObsNameInUseException>(() => test.Model.Scenes["Gaming"].CreateInputAsync("Camera", "color_source_v3").WithTimeoutAsync(Timeout));

            Assert.Same(test.Model.Inputs["Camera"], exception.HeldBy);
            Assert.Equal("OBS Studio refused creating input 'Camera' in scene 'Gaming': an input named 'Camera' exists already. Nothing was changed.", exception.Message);
        }

        [Fact]
        public async Task AnInput_WhoseNameIsTakenByAScene_IsANameInUseException_ThatSaysThatAnInputCanNotHaveTheNameOfAScene()
        {
            await using var test = await ByNameFixture.StartAsync();
            test.Responder.OnRequest = (type, data) => type == "CreateInput" ? (601, null) : null;

            var exception = await Assert.ThrowsAsync<ObsNameInUseException>(() => test.Model.Scenes["Gaming"].CreateInputAsync("Chatting", "color_source_v3").WithTimeoutAsync(Timeout));

            Assert.Same(test.Model.Scenes["Chatting"], exception.HeldBy);
            Assert.Contains("a scene named 'Chatting' exists already, and an input can not have the name of a scene", exception.Message);
        }

        [Fact]
        public async Task AnInput_WhoseNameIsTakenByAGroup_IsANameInUseException_ThatNamesTheGroup()
        {
            await using var test = await ByNameFixture.StartAsync(withGroup: true);
            var group = test.Model.Scenes["Gaming"].Items["Group"].Source;
            test.Responder.OnRequest = (type, data) => type == "CreateInput" ? (601, null) : null;

            // A group is not in the collection of scenes, and holds its name like any source.
            var exception = await Assert.ThrowsAsync<ObsNameInUseException>(() => test.Model.Scenes["Gaming"].CreateInputAsync("Group", "color_source_v3").WithTimeoutAsync(Timeout));

            Assert.Same(group, exception.HeldBy);
            Assert.Contains("a group named 'Group' exists already, and a group holds its name like any other source", exception.Message);
        }

        [Fact]
        public async Task AnInput_WhoseNameIsTakenByAnInputThatWasRemovedAndIsAlive_IsANameInUseException_ThatSaysWhatKeepsItAlive()
        {
            await using var test = await ByNameFixture.StartAsync();
            await test.DetachAsync("Overlay", test.Overlay);
            test.Responder.OnRequest = (type, data) => type == "CreateInput" ? (601, null) : null;

            var exception = await Assert.ThrowsAsync<ObsNameInUseException>(() => test.Model.Scenes["Gaming"].CreateInputAsync("Overlay", "color_source_v3").WithTimeoutAsync(Timeout));

            var overlay = test.FindDetached(test.Overlay);
            Assert.Same(overlay, exception.HeldBy);
            Assert.False(test.Model.Inputs.Contains("Overlay"));
            Assert.Contains("an input named 'Overlay' was removed, but OBS Studio keeps it alive because 2 scene items still show it", exception.Message);
            Assert.Contains("and a name stays taken until the last of them is gone", exception.Message);
        }

        [Fact]
        public async Task AName_ThatOBSStudioFreesAMomentLate_IsAskedForAgain_AndTheSceneIsMade()
        {
            await using var test = await ByNameFixture.StartAsync();

            // OBS Studio destroys a source that was removed a moment after the model can tell, and keeps its name taken until it has: the first answers say so, and nothing in the model holds the name.
            var attempts = 0;
            test.Responder.OnRequest = (type, data) =>
            {
                if (type != "CreateScene")
                {
                    return null;
                }

                return Interlocked.Increment(ref attempts) <= 2 ? (601, null) : (100, new { sceneUuid = test.MakeScene("Intro") });
            };

            var scene = await test.Model.CreateSceneAsync("Intro").WithTimeoutAsync(Timeout);

            Assert.Same(scene, test.Model.Scenes["Intro"]);
            Assert.Equal(3, test.Count("CreateScene"));
        }

        [Fact]
        public async Task AName_ThatOBSStudioFreesAMomentLate_IsAskedForAgain_AndTheInputIsMade()
        {
            await using var test = await ByNameFixture.StartAsync();
            var attempts = 0;
            test.Responder.OnRequest = (type, data) =>
            {
                if (type != "CreateInput")
                {
                    return null;
                }

                if (Interlocked.Increment(ref attempts) <= 2)
                {
                    return (601, null);
                }

                var (input, item) = test.MakeInput(test.Gaming, "Backdrop");
                return (100, new { inputUuid = input, sceneItemId = item });
            };

            var backdrop = await test.Model.Scenes["Gaming"].CreateInputAsync("Backdrop", "color_source_v3").WithTimeoutAsync(Timeout);

            Assert.Same(backdrop, test.Model.Inputs["Backdrop"]);
            Assert.Equal(3, test.Count("CreateInput"));
        }

        [Fact]
        public async Task AName_ThatNothingInTheModelHolds_IsAskedForAgain_UntilTheTimeForOBSStudioToCatchUpIsOver_AndThenRefused()
        {
            await using var test = await ByNameFixture.StartAsync();
            test.Responder.OnRequest = (type, data) => type == "CreateScene" ? (601, null) : null;

            var exception = await Assert.ThrowsAsync<ObsNameInUseException>(() => test.Model.CreateSceneAsync("Ghost").WithTimeoutAsync(Timeout));

            // The time is ConfirmationTimeout (300 ms here), and the name is asked for every 25 ms: more than twice, and not for ever.
            Assert.Null(exception.HeldBy);
            Assert.InRange(test.Count("CreateScene"), 3, 40);
        }

        [Fact]
        public async Task AName_ThatTheCallerGivesUpWhileItIsAskedForAgain_IsNotAskedForAgain()
        {
            await using var test = await ByNameFixture.StartAsync();
            using var giveUp = new CancellationTokenSource();
            test.Responder.OnRequest = (type, data) =>
            {
                if (type != "CreateScene")
                {
                    return null;
                }

                giveUp.Cancel();
                return (601, null);
            };

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => test.Model.CreateSceneAsync("Ghost", giveUp.Token).WithTimeoutAsync(Timeout));

            // What was refused made nothing, and the line is not held up: it was asked for once.
            await Task.Delay(200);
            Assert.Equal(1, test.Count("CreateScene"));
        }

        [Fact]
        public async Task AScene_WhoseNameIsHeldByAnObjectOfTheModel_IsNotAskedForAgain()
        {
            await using var test = await ByNameFixture.StartAsync();
            test.Responder.OnRequest = (type, data) => type == "CreateScene" ? (601, null) : null;

            var exception = await Assert.ThrowsAsync<ObsNameInUseException>(() => test.Model.CreateSceneAsync("Gaming").WithTimeoutAsync(Timeout));

            Assert.Same(test.Model.Scenes["Gaming"], exception.HeldBy);
            Assert.Equal(1, test.Count("CreateScene"));
        }

        [Fact]
        public async Task AnInput_WhoseNameIsTakenByAnInputThatWasRemovedAndIsAliveForOneSceneItem_SaysItInTheSingular()
        {
            await using var test = await ByNameFixture.StartAsync();
            test.RemoveItem(test.Chatting, 2);
            await test.Responder.SendEventAsync("SceneItemRemoved", new { sceneName = "Chatting", sceneUuid = test.Chatting, sourceName = "Overlay", sourceUuid = test.Overlay, sceneItemId = 2 }, 128);
            await ModelRig.EventuallyAsync(() => test.Model.Scenes["Chatting"].Items.Count == 1, "the scene item to be removed");
            await test.DetachAsync("Overlay", test.Overlay);
            test.Responder.OnRequest = (type, data) => type == "CreateInput" ? (601, null) : null;

            var exception = await Assert.ThrowsAsync<ObsNameInUseException>(() => test.Model.Scenes["Gaming"].CreateInputAsync("Overlay", "color_source_v3").WithTimeoutAsync(Timeout));

            Assert.Contains("because 1 scene item still shows it (scene item 3 of scene 'Gaming')", exception.Message);
        }

        [Fact]
        public async Task AnInput_InASceneThatWasRemoved_IsNotAskedFor()
        {
            await using var test = await ByNameFixture.StartAsync();
            var chatting = test.Model.Scenes["Chatting"];
            test.RemoveScene(test.Chatting);
            await ModelRig.EventuallyAsync(() => chatting.IsRemoved, "the scene to be removed");

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

            var exception = await Assert.ThrowsAsync<ObsNotConfirmedException>(() => test.Model.Scenes["Gaming"].CreateInputAsync("Logo", "color_source_v3").WithTimeoutAsync(Timeout));

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

            var logo = await test.Model.Scenes["Chatting"].CreateInputAsync("Logo", "color_source_v3").WithTimeoutAsync(Timeout);

            Assert.Same(logo, test.Model.Inputs["Logo"]);
            var items = logo.GetSceneItems();
            Assert.Single(items);
            Assert.NotEqual(99, items[0].Id);
        }

        [Fact]
        public async Task AnInput_OfAKindThatOBSStudioDoesNotKnow_IsAResponseException_WithItsCode()
        {
            await using var test = await ByNameFixture.StartAsync();
            test.Responder.OnRequest = (type, data) => type == "CreateInput" ? (605, null) : null;

            var exception = await Assert.ThrowsAsync<ObsResponseException>(() => test.Model.Scenes["Gaming"].CreateInputAsync("Logo", "no_such_kind").WithTimeoutAsync(Timeout));

            Assert.Equal(RequestStatusCode.InvalidInputKind, exception.ErrorCode);
        }

        [Fact]
        public async Task ASceneItem_IsReturnedWhenTheModelShowsIt_AndTheRequestSaysWhatWasAskedFor()
        {
            await using var test = await ByNameFixture.StartAsync();
            var scene = test.Model.Scenes["Scene"];
            var camera = test.Model.Inputs["Camera"];
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
            var scene = test.Model.Scenes["Scene"];
            var chatting = test.Model.Scenes["Chatting"];
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
            var second = await scene.AddAsync(test.Model.Inputs["Camera"]).WithTimeoutAsync(Timeout);
            var third = await scene.AddAsync(test.Model.Inputs["Camera"]).WithTimeoutAsync(Timeout);

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

            var exception = await Assert.ThrowsAsync<ObsNotConfirmedException>(() => test.Model.Scenes["Scene"].AddAsync(test.Model.Inputs["Camera"]).WithTimeoutAsync(Timeout));

            Assert.Contains("adding input 'Camera' to scene 'Scene'", exception.Message);
            Assert.Contains("the scene item is not in the model", exception.Message);
        }

        [Fact]
        public async Task ASceneItem_ThatWouldMakeASceneContainItself_IsAResponseException_WithItsCode()
        {
            await using var test = await ByNameFixture.StartAsync();
            test.Responder.OnRequest = (type, data) => type == "CreateSceneItem" ? (703, null) : null;

            var exception = await Assert.ThrowsAsync<ObsResponseException>(() => test.Model.Scenes["Chatting"].AddAsync(test.Model.Scenes["Gaming"]).WithTimeoutAsync(Timeout));

            Assert.Equal(RequestStatusCode.CannotAct, exception.ErrorCode);
            Assert.Equal(1, test.Count("CreateSceneItem"));
        }

        [Fact]
        public async Task ASceneItem_IsRefusedAtOnce_ForASourceThatIsNotAllowed()
        {
            await using var test = await ByNameFixture.StartAsync();
            var gaming = test.Model.Scenes["Gaming"];
            await using var other = await ByNameFixture.StartAsync();

            Assert.Throws<ArgumentNullException>(() => { _ = gaming.AddAsync(null!); });
            Assert.Throws<ArgumentException>(() => { _ = gaming.AddAsync(gaming); });
            var foreign = Assert.Throws<ArgumentException>(() => { _ = gaming.AddAsync(other.Model.Inputs["Camera"]); });
            Assert.Equal("source", foreign.ParamName);
            Assert.Equal(0, test.Count("CreateSceneItem"));
        }

        [Fact]
        public async Task ASceneItem_ForASourceThatWasRemoved_IsNotAskedFor()
        {
            await using var test = await ByNameFixture.StartAsync();
            await test.DetachAsync("Overlay", test.Overlay);
            var overlay = test.FindDetached(test.Overlay);

            var exception = await Assert.ThrowsAsync<ObsRemovedException>(() => test.Model.Scenes["Scene"].AddAsync(overlay).WithTimeoutAsync(Timeout));

            Assert.Equal(ObsObjectState.Detached, exception.State);
            Assert.Equal("input 'Overlay'", exception.Description);
            Assert.Equal(0, test.Count("CreateSceneItem"));
        }

        [Fact]
        public async Task ASceneItem_InASceneThatWasRemoved_IsNotAskedFor()
        {
            await using var test = await ByNameFixture.StartAsync();
            var chatting = test.Model.Scenes["Chatting"];
            test.RemoveScene(test.Chatting);
            await ModelRig.EventuallyAsync(() => chatting.IsRemoved, "the scene to be removed");

            var exception = await Assert.ThrowsAsync<ObsRemovedException>(() => chatting.AddAsync(test.Model.Inputs["Camera"]).WithTimeoutAsync(Timeout));

            Assert.Equal("scene 'Chatting'", exception.Description);
            Assert.Equal(0, test.Count("CreateSceneItem"));
        }

        [Fact]
        public async Task ASceneItem_ThatOBSStudioMadeInASceneThatWasRemovedMeanwhile_IsReadWhenTheModelLearnsThatTheSceneIsRemoved()
        {
            // The seed 273 of the random walks. OBS Studio removed the scene (the scene item of "Gaming" keeps it alive) before the request came, and the event that says so is on its way: the
            // model shows the scene as live, the request is written, and OBS Studio makes the scene item and sends no event for it (it sends none for a scene that it removed). The call ends
            // when the model learns that the scene is removed, and the scene items of that scene are read all the same, so that the model shows what OBS Studio holds.
            await using var test = await ByNameFixture.StartAsync(new ObsModelOptions { ConfirmationTimeout = TimeSpan.FromSeconds(20), WriteTimeout = TimeSpan.FromSeconds(5) });
            var chatting = test.Model.Scenes["Chatting"];
            var camera = test.Model.Inputs["Camera"];
            test.Responder.Change(world => world.Scenes.RemoveAll(scene => scene.Uuid == test.Chatting));
            test.Responder.OnRequest = (type, data) => type == "CreateSceneItem" ? (100, new { sceneItemId = test.MakeItem(test.Chatting, test.Camera, "Camera") }) : null;

            var add = chatting.AddAsync(camera);
            await ModelRig.EventuallyAsync(() => test.Count("CreateSceneItem") == 1, "the request to be sent");
            await test.Responder.SendEventAsync("SceneRemoved", new { sceneName = "Chatting", sceneUuid = test.Chatting, isGroup = false }, 4);

            // The call ends with ObsRemovedException when the model learns that the scene is removed before it shows the scene item, and with the scene item when the read of the scene items of the
            // removed scene (see above) is applied first: a race between two threads, and the model shows what OBS Studio holds either way.
            var outcome = await Record.ExceptionAsync(() => add.WithTimeoutAsync(Timeout));

            Assert.True(outcome is null or ObsRemovedException { State: ObsObjectState.Detached }, outcome?.ToString());
            await ModelRig.EventuallyAsync(() => chatting.Items.TryGet("Camera", out _), "the scene items of the removed scene to be read");
        }

        [Fact]
        public async Task AnInputOrASceneItem_IsRefusedAtOnce_ForAGroup_BecauseObsWebsocketMakesThemInScenesOnly()
        {
            await using var test = await ByNameFixture.StartAsync(withGroup: true);
            var group = (ObsScene)test.Model.Scenes["Gaming"].Items["Group"].Source;

            var input = await Assert.ThrowsAsync<InvalidOperationException>(() => group.CreateInputAsync("Logo", "color_source_v3").WithTimeoutAsync(Timeout));
            var item = await Assert.ThrowsAsync<InvalidOperationException>(() => group.AddAsync(test.Model.Inputs["Camera"]).WithTimeoutAsync(Timeout));

            Assert.Contains("The group 'Group' can not be given a new scene item: obs-websocket makes inputs and scene items in scenes only, not in a group. Nothing was changed.", input.Message);
            Assert.Equal(input.Message, item.Message);
            Assert.Equal(0, test.Count("CreateInput") + test.Count("CreateSceneItem"));
        }

        [Fact]
        public async Task TheSettingsOfAnInput_ThatAreNotAnObject_OrCanNotBeWritten_OrAreACancellationToken_AreAnArgumentException()
        {
            await using var test = await ByNameFixture.StartAsync();
            var scene = test.Model.Scenes["Scene"];
            using var source = new CancellationTokenSource();
            var cyclic = new Dictionary<string, object?>();
            cyclic["self"] = cyclic;

            // A token passed by position is taken for the settings (the parameter is an object so that new { width = 640 } can be passed): the call says so, and nothing was sent.
            var token = Assert.Throws<ArgumentException>(() => { _ = scene.CreateInputAsync("Logo", "color_source_v3", source.Token); });
            var number = Assert.Throws<ArgumentException>(() => { _ = scene.CreateInputAsync("Logo", "color_source_v3", 5); });
            var unwritable = Assert.Throws<ArgumentException>(() => { _ = scene.CreateInputAsync("Logo", "color_source_v3", cyclic); });

            Assert.Equal("settings", token.ParamName);
            Assert.Contains("Pass it by name (cancellationToken: token)", token.Message);
            Assert.Contains("must be an object", number.Message);
            Assert.Equal("settings", unwritable.ParamName);
            Assert.Contains("can not be written as JSON", unwritable.Message);
            Assert.NotNull(unwritable.InnerException);
            Assert.Equal(0, test.Count("CreateInput"));
        }
    }
}
