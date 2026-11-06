namespace OBSStudioClient.Tests.Simulation
{
    using System.Reflection;
    using System.Text.Json;
    using OBSStudioClient.Tests.Conformance;

    /// <summary>
    /// The requests of the simulation: the statuses and comments of obs-websocket 5.7.6 for what a client can get wrong, the request batches
    /// (variables included), what the simulation does with messages that obs-websocket does not accept, and the changes of someone else.
    /// </summary>
    public class SimObsRequestTests
    {
        private static readonly SimQuirks Ideal = SimQuirks.Ideal;

        private static (int Code, string? Comment) Refusal(SimHarness h, string type, object? data = null)
        {
            var response = h.Call(type, data);
            return (response.Code, response.Comment);
        }

        private static JsonElement[] Results(SimEntry batch) => batch.Data.EnumerateArray().ToArray();

        [Fact]
        public void AnUnknownRequestIsAnswered204_AndAnEmptyTypeIs203()
        {
            var h = new SimHarness(Ideal);

            Assert.Equal((204, "Your request type is not valid."), Refusal(h, "NoSuchRequest"));
            Assert.Equal((204, "Your request type is not valid."), Refusal(h, "GetStats"));
            Assert.Equal((203, "Your request's `requestType` may not be empty."), Refusal(h, string.Empty));
        }

        [Fact]
        public void GetVersionListsExactlyTheRequestsThatTheSimulationImplements()
        {
            var h = new SimHarness(Ideal);

            var version = h.Ok("GetVersion");

            var available = version.Data.GetProperty("availableRequests").EnumerateArray().Select(name => name.GetString()!).ToArray();
            Assert.Equal(SimRequests.Implemented, available);
            Assert.Equal("33.0.0", version.String("obsVersion"));
            Assert.Equal("5.7.6", version.String("obsWebSocketVersion"));
            Assert.Equal(1, version.Int("rpcVersion"));
            Assert.Equal("windows", version.String("platform"));
            Assert.Contains("png", version.Data.GetProperty("supportedImageFormats").EnumerateArray().Select(format => format.GetString()!));
            foreach (var type in available)
            {
                Assert.NotEqual(204, h.Call(type).Code);
            }
        }

        [Fact]
        public void TheImplementedRequestsAreRequestsOfTheProtocol()
        {
            var unknown = SimRequests.Implemented.Where(type => !ProtocolSnapshot.Current.Requests.ContainsKey(type)).ToArray();

            Assert.Empty(unknown);
            Assert.True(SimRequests.Implemented.Count >= 30);
        }

        [Fact]
        public void ARequestWithoutDataOrWithTheWrongData_IsRefusedWithTheCommentOfObsWebsocket()
        {
            var h = new SimHarness(Ideal);
            h.CreateInput(h.CreateScene("S"), "Mic", "wasapi_input_capture");

            Assert.Equal((301, "Your request data is missing or invalid (non-object)"), Refusal(h, "CreateScene"));
            Assert.Equal((300, "Your request is missing the `sceneName` field."), Refusal(h, "CreateScene", new { }));
            Assert.Equal((401, "The field value of `sceneName` must be a string."), Refusal(h, "CreateScene", new { sceneName = 5 }));
            Assert.Equal((403, "The field value of `sceneName` must not be empty."), Refusal(h, "CreateScene", new { sceneName = string.Empty }));
            Assert.Equal((401, "The field value of `inputMuted` must be boolean."), Refusal(h, "SetInputMute", new { inputName = "Mic", inputMuted = "yes" }));
            Assert.Equal((300, "Your request is missing the `inputMuted` field."), Refusal(h, "SetInputMute", new { inputName = "Mic" }));
            Assert.Equal((403, "The field value of `inputKind` must not be empty."), Refusal(h, "GetInputList", new { inputKind = string.Empty }));
            Assert.Equal((401, "The field value of `unversioned` must be boolean."), Refusal(h, "GetInputKindList", new { unversioned = "yes" }));
        }

        [Fact]
        public void ANameOrUuidThatIsNotThere_IsRefusedWith600AndTheCommentOfObsWebsocket()
        {
            var h = new SimHarness(Ideal);
            var uuid = Guid.NewGuid().ToString();

            Assert.Equal((300, "Your request must contain at least one of the following fields: `sceneName` with optional `canvasUuid` or `sceneUuid`."), Refusal(h, "GetSceneItemList", new { }));
            Assert.Equal((300, "Your request must contain at least one of the following fields: `inputName` with optional `canvasUuid` or `inputUuid`."), Refusal(h, "RemoveInput"));
            Assert.Equal((600, $"No source was found by the UUID of `{uuid}`."), Refusal(h, "GetSceneItemList", new { sceneUuid = uuid }));
            Assert.Equal((600, "No source was found by the name of `Nope` within the canvas `Main`."), Refusal(h, "GetSceneItemList", new { sceneName = "Nope" }));
            Assert.Equal((600, "No canvas was found by the UUID of `nope`."), Refusal(h, "GetSceneItemList", new { sceneName = "Scene", canvasUuid = "nope" }));
            Assert.Equal((600, "No source was found by the name of `Nope` within the canvas `Main`."), Refusal(h, "GetInputMute", new { inputName = "Nope" }));
            Assert.Equal((300, "Your request is missing the `sceneItemId` field."), Refusal(h, "GetSceneItemSource", new { sceneName = "Scene" }));
            Assert.Equal((402, "The field value of `sceneItemId` is below the minimum of `0.000000`"), Refusal(h, "GetSceneItemSource", new { sceneName = "Scene", sceneItemId = -1 }));
            Assert.Equal((600, "No scene items were found in scene `Scene` with the ID `99`."), Refusal(h, "GetSceneItemSource", new { sceneName = "Scene", sceneItemId = 99 }));
            Assert.Equal((401, "The field value of `sceneItemId` must be a number."), Refusal(h, "GetSceneItemSource", new { sceneName = "Scene", sceneItemId = "1" }));
        }

        [Fact]
        public void AUuidWinsOverAName_AndAUuidThatCannotBeUsedFallsBackToTheName()
        {
            var h = new SimHarness(Ideal);
            var scene = h.CreateScene("Real");

            Assert.True(h.Call("GetSceneItemList", new { sceneName = "Nope", sceneUuid = scene.ToString() }).Succeeded);
            Assert.Equal(600, h.Call("GetSceneItemList", new { sceneName = "Real", sceneUuid = Guid.NewGuid().ToString() }).Code);
            Assert.True(h.Call("GetSceneItemList", new { sceneName = "Real", sceneUuid = 5 }).Succeeded);
            Assert.True(h.Call("GetSceneItemList", new { sceneName = "Real", sceneUuid = string.Empty }).Succeeded);
        }

        [Fact]
        public void ANameThatIsTaken_IsRefusedWith601_AndTheCommentSaysWhichName()
        {
            var h = new SimHarness(Ideal);
            var scene = h.CreateScene("A");
            h.CreateScene("B");
            h.CreateInput(scene, "X");
            h.CreateInput(scene, "Y");

            Assert.Equal((601, "A source already exists by that scene name."), Refusal(h, "CreateScene", new { sceneName = "A" }));
            Assert.Equal((601, "A source already exists by that new scene name."), Refusal(h, "SetSceneName", new { sceneName = "A", newSceneName = "B" }));
            Assert.Equal((601, "A source already exists by that new scene name."), Refusal(h, "SetSceneName", new { sceneName = "A", newSceneName = "A" }));
            Assert.Equal((601, "A source already exists by that input name."), Refusal(h, "CreateInput", new { sceneName = "A", inputName = "X", inputKind = "color_source_v3" }));
            Assert.Equal((601, "A source already exists by that new input name."), Refusal(h, "SetInputName", new { inputName = "X", newInputName = "Y" }));
            Assert.Equal((601, "A source already exists by that new input name."), Refusal(h, "SetInputName", new { inputName = "X", newInputName = "A" }));
            Assert.True(h.Call("SetSceneName", new { sceneName = "A", newSceneName = "X" }).Succeeded, "A scene can take the name of an input.");
        }

        [Fact]
        public void ASourceOfTheWrongTypeIsRefusedWith602()
        {
            var h = new SimHarness(Ideal);
            var scene = h.CreateScene("A");
            h.CreateInput(scene, "Mic", "wasapi_input_capture");

            Assert.Equal((602, "The specified source is not an input."), Refusal(h, "GetInputMute", new { inputName = "A" }));
            Assert.Equal((602, "The specified source is not a scene."), Refusal(h, "GetSceneItemList", new { sceneName = "Mic" }));
            Assert.Equal((602, "The specified source is not a scene."), Refusal(h, "CreateInput", new { sceneName = "Mic", inputName = "Z", inputKind = "color_source_v3" }));
        }

        [Fact]
        public void AnInputOfAKindThatDoesNotExist_IsRefused605_AfterTheNameIsChecked()
        {
            var h = new SimHarness(Ideal);
            var scene = h.CreateScene("A");

            var refused = h.Call("CreateInput", new { sceneUuid = scene.ToString(), inputName = "Z", inputKind = "no_such_source" });

            Assert.Equal(605, refused.Code);
            Assert.StartsWith("Your specified input kind is not supported by OBS.", refused.Comment);
            Assert.Equal(601, h.Call("CreateInput", new { sceneUuid = scene.ToString(), inputName = "A", inputKind = "no_such_source" }).Code);
        }

        [Fact]
        public void TheVolumeOfAnInput_IsCheckedTheWayObsWebsocketChecksIt()
        {
            var h = new SimHarness(Ideal);
            var scene = h.CreateScene("A");
            h.CreateInput(scene, "Mic", "wasapi_input_capture");

            Assert.Equal((404, "You may only specify one volume field."), Refusal(h, "SetInputVolume", new { inputName = "Mic", inputVolumeMul = 1, inputVolumeDb = 0 }));
            Assert.Equal((300, "You must specify one volume field."), Refusal(h, "SetInputVolume", new { inputName = "Mic" }));
            Assert.Equal((402, "The field value of `inputVolumeMul` is above the maximum of `20.000000`"), Refusal(h, "SetInputVolume", new { inputName = "Mic", inputVolumeMul = 21 }));
            Assert.Equal((402, "The field value of `inputVolumeMul` is below the minimum of `0.000000`"), Refusal(h, "SetInputVolume", new { inputName = "Mic", inputVolumeMul = -1 }));
            Assert.Equal((402, "The field value of `inputVolumeDb` is below the minimum of `-100.000000`"), Refusal(h, "SetInputVolume", new { inputName = "Mic", inputVolumeDb = -101 }));
            Assert.Equal((402, "The field value of `inputVolumeDb` is above the maximum of `26.000000`"), Refusal(h, "SetInputVolume", new { inputName = "Mic", inputVolumeDb = 27 }));
            Assert.Equal((401, "The field value of `inputVolumeMul` must be a number."), Refusal(h, "SetInputVolume", new { inputName = "Mic", inputVolumeMul = "loud" }));

            h.Ok("SetInputVolume", new { inputName = "Mic", inputVolumeMul = 0 });
            var silent = h.Ok("GetInputVolume", new { inputName = "Mic" });
            Assert.Equal(0.0, silent.Data.GetProperty("inputVolumeMul").GetDouble());
            Assert.Equal(-100.0, silent.Data.GetProperty("inputVolumeDb").GetDouble());
        }

        [Fact]
        public void ASceneItemIndexIsCheckedForItsRange()
        {
            var h = new SimHarness(Ideal);
            var scene = h.CreateScene("A");
            var (_, item) = h.CreateInput(scene, "X");

            Assert.Equal(402, h.Call("SetSceneItemIndex", new { sceneUuid = scene.ToString(), sceneItemId = item, sceneItemIndex = 9000 }).Code);
            Assert.Equal(402, h.Call("SetSceneItemIndex", new { sceneUuid = scene.ToString(), sceneItemId = item, sceneItemIndex = -1 }).Code);
            Assert.Equal(300, h.Call("SetSceneItemIndex", new { sceneUuid = scene.ToString(), sceneItemId = item }).Code);
            Assert.Equal(300, h.Call("SetSceneItemEnabled", new { sceneUuid = scene.ToString(), sceneItemId = item }).Code);
        }

        [Fact]
        public void StudioModeIsNotSimulated()
        {
            var h = new SimHarness(Ideal);

            Assert.False(h.Ok("GetStudioModeEnabled").Bool("studioModeEnabled"));
            var preview = h.Call("GetCurrentPreviewScene");
            Assert.Equal(506, preview.Code);
            Assert.Null(preview.Comment);
            var list = h.Ok("GetSceneList");
            Assert.Equal(JsonValueKind.Null, list.Data.GetProperty("currentPreviewSceneName").ValueKind);
            Assert.Equal(JsonValueKind.Null, list.Data.GetProperty("currentPreviewSceneUuid").ValueKind);
            Assert.Empty(h.Ok("GetGroupList").Data.GetProperty("groups").EnumerateArray());
        }

        [Fact]
        public void TheInputKindsAreListedWithAndWithoutTheirVersion_AndTheListOfInputsCanBeFiltered()
        {
            var h = new SimHarness(Ideal);
            var scene = h.CreateScene("S");
            h.CreateInput(scene, "A", "color_source_v3");
            h.CreateInput(scene, "B", "image_source");
            h.CreateInput(scene, "C", "color_source_v3");

            var versioned = h.Ok("GetInputKindList").Data.GetProperty("inputKinds").EnumerateArray().Select(kind => kind.GetString()!).ToArray();
            var unversioned = h.Ok("GetInputKindList", new { unversioned = true }).Data.GetProperty("inputKinds").EnumerateArray().Select(kind => kind.GetString()!).ToArray();

            Assert.Contains("color_source_v3", versioned);
            Assert.Contains("color_source", unversioned);
            Assert.DoesNotContain("color_source_v3", unversioned);
            Assert.Equal(versioned.Length, unversioned.Length);
            Assert.Equal(["A", "C"], h.Ok("GetInputList", new { inputKind = "color_source_v3" }).Strings("inputs", "inputName"));
            Assert.Equal(["B"], h.Ok("GetInputList", new { inputKind = "image_source" }).Strings("inputs", "inputName"));
            Assert.Empty(h.Ok("GetInputList", new { inputKind = "unknown" }).Data.GetProperty("inputs").EnumerateArray());
        }

        [Fact]
        public void GetInputSettingsGivesTheSettingsThatTheInputWasCreatedWith_WithoutTheDefaults()
        {
            var h = new SimHarness(Ideal);
            var scene = h.CreateScene("S");
            h.Ok("CreateInput", new { sceneUuid = scene.ToString(), inputName = "Colour", inputKind = "color_source_v3", inputSettings = new { width = 640, height = 360, color = 4294901760 } });
            h.CreateInput(scene, "Plain", "image_source");

            var settings = h.Ok("GetInputSettings", new { inputName = "Colour" });
            var plain = h.Ok("GetInputSettings", new { inputName = "Plain" });

            Assert.Equal("color_source_v3", settings.String("inputKind"));
            Assert.Equal(["color", "height", "width"], settings.Data.GetProperty("inputSettings").EnumerateObject().Select(member => member.Name).ToArray());
            Assert.Empty(plain.Data.GetProperty("inputSettings").EnumerateObject());
            Assert.Equal(360.0, h.Ok("GetSceneItemList", new { sceneUuid = scene.ToString() }).Data.GetProperty("sceneItems")[0].GetProperty("sceneItemTransform").GetProperty("sourceHeight").GetDouble());
        }

        [Fact]
        public void AnItemThatIsCreatedDisabledIsAnnouncedAsEnabledThenDisabled()
        {
            var h = new SimHarness(Ideal);
            var scene = h.CreateScene("S");
            h.Settle();
            h.ClearLog();

            h.Ok("CreateInput", new { sceneUuid = scene.ToString(), inputName = "X", inputKind = "color_source_v3", sceneItemEnabled = false });
            h.Settle();

            Assert.Equal(["InputCreated", "SceneItemCreated", "SceneItemEnableStateChanged", "SceneItemSelected"], h.EventNames());
            var item = h.Ok("GetSceneItemList", new { sceneUuid = scene.ToString() }).Data.GetProperty("sceneItems")[0];
            Assert.False(item.GetProperty("sceneItemEnabled").GetBoolean());
        }

        [Fact]
        public void TheEventsOfAnItemInASceneThatWasRemoved_AreNotAnnounced()
        {
            var h = new SimHarness(Ideal);
            var parent = h.CreateScene("P");
            var child = h.CreateScene("C");
            h.CreateSceneItem(parent, child);
            var (_, item) = h.CreateInput(child, "Inner");
            h.Ok("RemoveScene", new { sceneUuid = child.ToString() });
            h.Settle();
            h.ClearLog();

            h.Ok("SetSceneItemEnabled", new { sceneUuid = child.ToString(), sceneItemId = item, sceneItemEnabled = false });
            h.Ok("SetSceneItemIndex", new { sceneUuid = child.ToString(), sceneItemId = item, sceneItemIndex = 0 });
            h.CreateInput(child, "Another");
            h.Ok("RemoveSceneItem", new { sceneUuid = child.ToString(), sceneItemId = item });
            h.Settle();

            Assert.Equal(["InputCreated", "InputRemoved"], h.EventNames());
            Assert.Equal(604, h.Call("SetCurrentProgramScene", new { sceneUuid = child.ToString() }).Code);
            h.Sim.CheckInvariants();
        }

        [Fact]
        public void ASecondRemovalOfAnInputThatWasRemovedSucceedsWithoutAnEvent_ButASceneCannotBeRemovedTwice()
        {
            var h = new SimHarness(Ideal);
            var parent = h.CreateScene("P");
            var child = h.CreateScene("C");
            h.CreateSceneItem(parent, child);
            var (input, _) = h.CreateInput(parent, "X");
            h.Ok("RemoveInput", new { inputUuid = input.ToString() });
            h.Ok("RemoveScene", new { sceneUuid = child.ToString() });
            h.Settle();
            h.ClearLog();

            Assert.True(h.Call("RemoveInput", new { inputUuid = input.ToString() }).Succeeded);
            Assert.Equal(600, h.Call("RemoveScene", new { sceneUuid = child.ToString() }).Code);
            h.Settle();

            Assert.Empty(h.Events());
        }

        [Fact]
        public void AnInputThatWasRemovedCanStillBeRenamedAndShownInAnotherScene()
        {
            var h = new SimHarness(Ideal);
            var one = h.CreateScene("One");
            var two = h.CreateScene("Two");
            var (x, _) = h.CreateInput(one, "X");
            h.Ok("RemoveInput", new { inputUuid = x.ToString() });

            h.Ok("SetInputName", new { inputUuid = x.ToString(), newInputName = "Renamed" });
            var item = h.CreateSceneItem(two, x);

            Assert.Equal(["Renamed"], h.InputNames());
            Assert.Equal([(item, 0, "Renamed")], h.Items(two));
            h.Sim.CheckInvariants();
        }

        [Fact]
        public void ABatchRunsItsRequestsInOrder_AndAnswersWithTheResultsInOrder()
        {
            var h = new SimHarness(Ideal);

            var batch = h.CallBatch(
            [
                new { requestType = "CreateScene", requestId = "a", requestData = new { sceneName = "One" } },
                new { requestType = "CreateScene", requestData = new { sceneName = "Two" } },
                new { requestType = "GetSceneList", requestId = "c" },
            ]);

            Assert.True(batch.IsBatchResponse);
            Assert.Equal("batch-1", batch.RequestId);
            var results = Results(batch);
            Assert.Equal(["CreateScene", "CreateScene", "GetSceneList"], results.Select(result => result.GetProperty("requestType").GetString()!).ToArray());
            Assert.Equal("a", results[0].GetProperty("requestId").GetString());
            Assert.False(results[1].TryGetProperty("requestId", out _), "A request without an ID gets no ID in its result.");
            Assert.Equal("c", results[2].GetProperty("requestId").GetString());
            Assert.All(results, result => Assert.True(result.GetProperty("requestStatus").GetProperty("result").GetBoolean()));
            Assert.Equal(["Two", "One", "Scene"], results[2].GetProperty("responseData").GetProperty("scenes").EnumerateArray().Select(scene => scene.GetProperty("sceneName").GetString()!).ToArray());
        }

        [Fact]
        public void ABatchThatHaltsOnFailure_AnswersOnlyUntilTheRequestThatFailed()
        {
            var h = new SimHarness(Ideal);

            var halting = Results(h.CallBatch(
                [
                    new { requestType = "CreateScene", requestData = new { sceneName = "One" } },
                    new { requestType = "RemoveScene", requestData = new { sceneName = "Nope" } },
                    new { requestType = "CreateScene", requestData = new { sceneName = "Never" } },
                ],
                haltOnFailure: true));
            var continuing = Results(h.CallBatch(
                [
                    new { requestType = "RemoveScene", requestData = new { sceneName = "Nope" } },
                    new { requestType = "CreateScene", requestData = new { sceneName = "Two" } },
                ],
                haltOnFailure: false));

            Assert.Equal(2, halting.Length);
            Assert.Equal(600, halting[1].GetProperty("requestStatus").GetProperty("code").GetInt32());
            Assert.Equal(2, continuing.Length);
            Assert.Equal(["Two", "One", "Scene"], h.Sim.Snapshot().Scenes.Select(scene => scene.Name).ToArray());
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void ASerialBatchPassesValuesFromOneRequestToTheNext_WithOutputAndInputVariables(int executionType)
        {
            var h = new SimHarness(Ideal);

            var batch = h.CallBatch(
            [
                new { requestType = "CreateScene", requestData = new { sceneName = "S" }, outputVariables = new { newScene = "sceneUuid" } },
                new
                {
                    requestType = "CreateInput",
                    requestData = new { inputName = "X", inputKind = "color_source_v3" },
                    inputVariables = new { sceneUuid = "newScene" },
                    outputVariables = new { itemId = "sceneItemId" },
                },
                new { requestType = "SetSceneItemEnabled", requestData = new { sceneItemEnabled = false }, inputVariables = new { sceneUuid = "newScene", sceneItemId = "itemId" } },
            ],
            executionType: executionType);

            var results = Results(batch);
            Assert.All(results, result => Assert.True(result.GetProperty("requestStatus").GetProperty("result").GetBoolean(), result.ToString()));
            var scene = h.Sim.Snapshot().Scenes.Single(candidate => candidate.Name == "S");
            Assert.False(scene.Items.Single().Enabled);
        }

        [Fact]
        public void TheVariablesOfABatchCanStartWithValues_AndAVariableThatIsNotThereIsLeftOut()
        {
            var h = new SimHarness(Ideal);

            var batch = h.CallBatch(
            [
                new { requestType = "CreateScene", requestData = new { }, inputVariables = new { sceneName = "name" } },
                new { requestType = "CreateScene", requestData = new { sceneName = "Fixed" }, inputVariables = new { sceneName = "nothing" } },
                new { requestType = "CreateScene", requestData = new { }, inputVariables = new { sceneName = "nothing" } },
            ],
            variables: new { name = "FromVariable" });

            var results = Results(batch);
            Assert.True(results[0].GetProperty("requestStatus").GetProperty("result").GetBoolean());
            Assert.True(results[1].GetProperty("requestStatus").GetProperty("result").GetBoolean());
            Assert.Equal(301, results[2].GetProperty("requestStatus").GetProperty("code").GetInt32());
            Assert.Contains(h.Sim.Snapshot().Scenes, scene => scene.Name == "FromVariable");
            Assert.Contains(h.Sim.Snapshot().Scenes, scene => scene.Name == "Fixed");
        }

        [Fact]
        public void ARequestWithoutRequestData_DoesNotGetItsFieldsFromTheVariables()
        {
            var h = new SimHarness(Ideal);

            // obs-websocket does not look at the variables of a request without request data (an object): it fails for the data that is not there.
            var batch = h.CallBatch(
            [
                new { requestType = "CreateScene", inputVariables = new { sceneName = "name" } },
                new { requestType = "CreateScene", requestData = "not an object", inputVariables = new { sceneName = "name" } },
            ],
            variables: new { name = "FromVariable" });

            var results = Results(batch);
            Assert.All(results, result => Assert.Equal(301, result.GetProperty("requestStatus").GetProperty("code").GetInt32()));
            Assert.DoesNotContain(h.Sim.Snapshot().Scenes, scene => scene.Name == "FromVariable");
        }

        [Fact]
        public void WithoutAnyVariablesYet_TheInputVariablesOfARequestDoNothing()
        {
            var h = new SimHarness(Ideal);

            var batch = h.CallBatch([new { requestType = "CreateScene", requestData = new { sceneName = "Own" }, inputVariables = new { sceneName = "name" } }]);

            Assert.True(Results(batch)[0].GetProperty("requestStatus").GetProperty("result").GetBoolean());
            Assert.Contains(h.Sim.Snapshot().Scenes, scene => scene.Name == "Own");
        }

        [Fact]
        public void ABatchRequestWithATypeThatIsNotAString_IsAnswered203()
        {
            var h = new SimHarness(Ideal);

            var batch = h.CallBatch([new { requestType = 5 }, new { requestData = new { } }]);

            var results = Results(batch);
            Assert.Equal(["", ""], results.Select(result => result.GetProperty("requestType").GetString()!).ToArray());
            Assert.All(results, result => Assert.Equal(203, result.GetProperty("requestStatus").GetProperty("code").GetInt32()));
        }

        [Fact]
        public void AParallelBatchAnswersWithTheTypeAndIdOfEachRequestInOrder_ButTheDataCanBeThatOfAnother()
        {
            var misordered = false;
            for (var seed = 1; seed <= 40 && !misordered; seed++)
            {
                var h = new SimHarness(Ideal with { ParallelBatchMisorders = true, ReorderConcurrentRequests = true, ExecutionJitter = new SimRange(0, 50) }, seed);
                var batch = h.CallBatch(
                [
                    new { requestType = "CreateScene", requestId = "r0", requestData = new { sceneName = "A" } },
                    new { requestType = "GetStudioModeEnabled", requestId = "r1" },
                    new { requestType = "GetCurrentProgramScene", requestId = "r2" },
                    new { requestType = "GetGroupList", requestId = "r3" },
                ],
                executionType: 2);

                var results = Results(batch);
                Assert.Equal(["r0", "r1", "r2", "r3"], results.Select(result => result.GetProperty("requestId").GetString()!).ToArray());
                Assert.Equal(["CreateScene", "GetStudioModeEnabled", "GetCurrentProgramScene", "GetGroupList"], results.Select(result => result.GetProperty("requestType").GetString()!).ToArray());
                var shapes = results.Select(result => result.TryGetProperty("responseData", out var data) ? string.Join(",", data.EnumerateObject().Select(member => member.Name)) : string.Empty).ToArray();
                var inOrder = new[] { "sceneUuid", "studioModeEnabled", "currentProgramSceneName,currentProgramSceneUuid,sceneName,sceneUuid", "groups" };
                misordered = !shapes.SequenceEqual(inOrder);
                Assert.Equal(inOrder.OrderBy(shape => shape, StringComparer.Ordinal), shapes.OrderBy(shape => shape, StringComparer.Ordinal));
            }

            Assert.True(misordered, "No seed misordered the results of a parallel batch.");
        }

        [Fact]
        public void AParallelBatchIsAnsweredInOrder_WithoutTheQuirk()
        {
            var h = new SimHarness(Ideal with { ParallelBatchMisorders = false, ReorderConcurrentRequests = true, ExecutionJitter = new SimRange(0, 50) });

            var batch = h.CallBatch(
            [
                new { requestType = "GetStudioModeEnabled" },
                new { requestType = "GetGroupList" },
            ],
            executionType: 2);

            var results = Results(batch);
            Assert.True(results[0].GetProperty("responseData").TryGetProperty("studioModeEnabled", out _));
            Assert.True(results[1].GetProperty("responseData").TryGetProperty("groups", out _));
        }

        [Fact]
        public void TheRequestsOfAParallelBatchRunInTheOrderOfTheirJitter_SoARequestCanReadBeforeAnotherWrites()
        {
            var readBefore = false;
            var readAfter = false;
            for (var seed = 1; seed <= 60; seed++)
            {
                var h = new SimHarness(Ideal with { ReorderConcurrentRequests = true, ExecutionJitter = new SimRange(0, 30) }, seed);
                var scene = h.CreateScene("A");

                var batch = h.CallBatch(
                [
                    new { requestType = "RemoveScene", requestId = "write", requestData = new { sceneUuid = scene.ToString() } },
                    new { requestType = "GetSceneItemList", requestId = "read", requestData = new { sceneUuid = scene.ToString() } },
                ],
                executionType: 2);

                var results = Results(batch);
                Assert.Equal(["read", "write"], results.Select(result => result.GetProperty("requestId").GetString()!).Order(StringComparer.Ordinal).ToArray());
                var read = results.Single(result => result.GetProperty("requestId").GetString() == "read");
                var code = read.GetProperty("requestStatus").GetProperty("code").GetInt32();
                readBefore |= code == 100;
                readAfter |= code == 600;
            }

            Assert.True(readBefore, "The read never ran before the removal.");
            Assert.True(readAfter, "The read never ran after the removal.");
        }

        [Fact]
        public void TheRequestsOfAParallelBatchRunInTheOrderOfTheBatch_WhenRequestsKeepTheirOrder()
        {
            for (var seed = 1; seed <= 20; seed++)
            {
                var h = new SimHarness(Ideal, seed);
                var scene = h.CreateScene("A");

                var batch = h.CallBatch(
                [
                    new { requestType = "GetSceneItemList", requestData = new { sceneUuid = scene.ToString() } },
                    new { requestType = "RemoveScene", requestData = new { sceneUuid = scene.ToString() } },
                ],
                executionType: 2);

                Assert.Equal(100, Results(batch)[0].GetProperty("requestStatus").GetProperty("code").GetInt32());
            }
        }

        [Fact]
        public void ABatchThatContainsARequestThatNeverGetsAnAnswer_NeverGetsAnAnswer()
        {
            var h = new SimHarness(Ideal with { HangOnRemovedSceneRename = true });
            var parent = h.CreateScene("P");
            var child = h.CreateScene("C");
            h.CreateSceneItem(parent, child);
            h.Ok("RemoveScene", new { sceneUuid = child.ToString() });
            h.Settle();
            h.ClearLog();

            var id = h.SendBatch(
            [
                new { requestType = "CreateScene", requestData = new { sceneName = "Before" } },
                new { requestType = "SetSceneName", requestData = new { sceneUuid = child.ToString(), newSceneName = "X" } },
                new { requestType = "CreateScene", requestData = new { sceneName = "After" } },
            ]);
            h.Run(10000);

            Assert.DoesNotContain(h.Log, entry => entry.RequestId == id);
            Assert.Contains(h.Sim.Snapshot().Scenes, scene => scene.Name == "Before");
            Assert.DoesNotContain(h.Sim.Snapshot().Scenes, scene => scene.Name == "After");
        }

        [Theory]
        [InlineData("not json", 4002)]
        [InlineData("[1,2]", 4002)]
        [InlineData("{\"d\":{}}", 4006)]
        [InlineData("{\"op\":\"6\",\"d\":{}}", 4006)]
        [InlineData("{\"op\":null,\"d\":{}}", 4006)]
        [InlineData("{\"op\":6}", 4003)]
        [InlineData("{\"op\":6,\"d\":5}", 4004)]
        [InlineData("{\"op\":6,\"d\":{\"requestType\":\"GetVersion\"}}", 4003)]
        [InlineData("{\"op\":6,\"d\":{\"requestId\":\"1\"}}", 4003)]
        [InlineData("{\"op\":6,\"d\":{\"requestId\":\"1\",\"requestType\":5}}", 4004)]
        [InlineData("{\"op\":8,\"d\":{\"requests\":[]}}", 4003)]
        [InlineData("{\"op\":8,\"d\":{\"requestId\":\"1\"}}", 4003)]
        [InlineData("{\"op\":8,\"d\":{\"requestId\":\"1\",\"requests\":{}}}", 4004)]
        [InlineData("{\"op\":8,\"d\":{\"requestId\":\"1\",\"requests\":[],\"executionType\":\"0\"}}", 4004)]
        [InlineData("{\"op\":8,\"d\":{\"requestId\":\"1\",\"requests\":[],\"executionType\":7}}", 4005)]
        [InlineData("{\"op\":8,\"d\":{\"requestId\":\"1\",\"requests\":[],\"haltOnFailure\":1}}", 4004)]
        [InlineData("{\"op\":8,\"d\":{\"requestId\":\"1\",\"requests\":[],\"executionType\":2,\"variables\":{}}}", 4012)]
        [InlineData("{\"op\":99,\"d\":{}}", 4006)]
        public void AMessageThatObsWebsocketDoesNotAccept_GetsNoResponse_AndIsNotedWithTheCloseCodeThatItWouldUse(string message, int closeCode)
        {
            var sim = new SimObs(Ideal);

            sim.Receive(message);
            sim.Advance(TimeSpan.FromSeconds(1));

            Assert.Empty(sim.TakeDue());
            Assert.Equal(closeCode, Assert.Single(sim.TakeViolations()).CloseCode);
            Assert.Empty(sim.TakeViolations());
        }

        [Fact]
        public void TheRequestIdThatIsSentIsTheRequestIdThatIsAnswered_WhateverItsType()
        {
            var sim = new SimObs(Ideal);

            sim.Receive("{\"op\":6,\"d\":{\"requestId\":7,\"requestType\":\"GetVersion\"}}");
            sim.Receive("{\"op\":6,\"d\":{\"requestId\":null,\"requestType\":\"GetVersion\"}}");
            sim.Advance(TimeSpan.Zero);
            var messages = sim.TakeDue();

            Assert.Equal(2, messages.Count);
            using var first = JsonDocument.Parse(messages[0].Json);
            using var second = JsonDocument.Parse(messages[1].Json);
            Assert.Equal(7, first.RootElement.GetProperty("d").GetProperty("requestId").GetInt32());
            Assert.Equal(JsonValueKind.Null, second.RootElement.GetProperty("d").GetProperty("requestId").ValueKind);
            Assert.Empty(sim.TakeViolations());
        }

        [Fact]
        public void ARequestDataThatIsNotAnObjectIsTreatedAsNoData()
        {
            var sim = new SimObs(Ideal);

            sim.Receive("{\"op\":6,\"d\":{\"requestId\":\"1\",\"requestType\":\"CreateScene\",\"requestData\":[1]}}");
            sim.Advance(TimeSpan.Zero);

            using var response = JsonDocument.Parse(sim.TakeDue().Single().Json);
            Assert.Equal(301, response.RootElement.GetProperty("d").GetProperty("requestStatus").GetProperty("code").GetInt32());
        }

        [Fact]
        public void TheMessagesOfTheSimulationAreCompactJsonWithTheMembersInAlphabeticalOrder()
        {
            var h = new SimHarness(Ideal);

            h.CreateScene("S");
            h.Settle();

            Assert.StartsWith("{\"d\":{\"requestId\":", h.Log.First(entry => entry.IsResponse).Message.Json);
            Assert.StartsWith("{\"d\":{\"eventData\":{\"isGroup\":false,\"sceneName\":\"S\",\"sceneUuid\":\"", h.Events("SceneCreated").First().Message.Json);
            Assert.EndsWith("\"eventIntent\":4,\"eventType\":\"SceneCreated\"},\"op\":5}", h.Events("SceneCreated").First().Message.Json);
        }

        [Fact]
        public void TheCodesThatTheSimulationUsesAreTheCodesOfTheProtocol()
        {
            foreach (var (type, enumeration) in new[] { (typeof(SimStatus), "RequestStatus"), (typeof(SimCloseCode), "WebSocketCloseCode"), (typeof(SimOp), "WebSocketOpCode") })
            {
                var protocol = ProtocolSnapshot.Current.Enums[enumeration];
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static).Where(field => field.IsLiteral))
                {
                    Assert.True(protocol.TryGetValue(field.Name, out var value), $"{enumeration} of the protocol has no {field.Name}.");
                    Assert.Equal((int)field.GetRawConstantValue()!, (int)value!.Value);
                }
            }

            Assert.Equal(4095, SimIntent.All);
            Assert.Equal(4, SimIntent.Scenes);
            Assert.Equal(8, SimIntent.Inputs);
            Assert.Equal(16, SimIntent.Transitions);
            Assert.Equal(128, SimIntent.SceneItems);
        }

        [Fact]
        public void ChangesOfSomeoneElse_AreMadeWithTheSameEventsAsTheRequest()
        {
            var viaRequest = new SimHarness(Ideal, seed: 5);
            var viaExternal = new SimHarness(Ideal, seed: 5);

            var sceneByRequest = viaRequest.CreateScene("S");
            var (inputByRequest, itemByRequest) = viaRequest.CreateInput(sceneByRequest, "X", "wasapi_input_capture");
            viaRequest.Ok("SetInputMute", new { inputUuid = inputByRequest.ToString(), inputMuted = true });
            viaRequest.Ok("SetSceneItemEnabled", new { sceneUuid = sceneByRequest.ToString(), sceneItemId = itemByRequest, sceneItemEnabled = false });
            viaRequest.Ok("RemoveSceneItem", new { sceneUuid = sceneByRequest.ToString(), sceneItemId = itemByRequest });
            viaRequest.Settle();

            var external = viaExternal.Sim.External;
            var sceneByExternal = external.CreateScene("S");
            var (inputByExternal, itemByExternal) = external.CreateInput(sceneByExternal, "X", "wasapi_input_capture");
            external.SetMute(inputByExternal, true);
            external.SetItemEnabled(sceneByExternal, itemByExternal, false);
            external.RemoveItem(sceneByExternal, itemByExternal);
            viaExternal.Settle();

            Assert.Equal(viaRequest.EventNames(), viaExternal.EventNames());
            Assert.Equal(viaRequest.Sim.Snapshot().ToString(), viaExternal.Sim.Snapshot().ToString());
            Assert.Equal(sceneByRequest, sceneByExternal);
        }

        [Fact]
        public void ChangesOfSomeoneElse_AreRefusedWithAnInvalidOperationExceptionThatSaysWhatObsStudioWouldAnswer()
        {
            var sim = new SimObs(Ideal);
            var external = sim.External;
            var scene = external.CreateScene("S");
            var (input, item) = external.CreateInput(scene, "X", "color_source_v3");

            Assert.Contains("601", Assert.Throws<InvalidOperationException>(() => external.CreateScene("S")).Message);
            Assert.Contains("601", Assert.Throws<InvalidOperationException>(() => external.CreateInput(scene, "X", "color_source_v3")).Message);
            Assert.Contains("605", Assert.Throws<InvalidOperationException>(() => external.CreateInput(scene, "Y", "no_such_kind")).Message);
            Assert.Contains("600", Assert.Throws<InvalidOperationException>(() => external.RemoveScene(Guid.NewGuid())).Message);
            Assert.Contains("602", Assert.Throws<InvalidOperationException>(() => external.RemoveScene(input)).Message);
            Assert.Contains("600", Assert.Throws<InvalidOperationException>(() => external.RemoveItem(scene, 99)).Message);
            Assert.Contains("604", Assert.Throws<InvalidOperationException>(() => external.SetMute(input, true)).Message);
            Assert.Contains("403", Assert.Throws<InvalidOperationException>(() => external.RenameScene(scene, string.Empty)).Message);
            Assert.Contains("603", Assert.Throws<InvalidOperationException>(() =>
            {
                external.RemoveScene(sim.Snapshot().Scenes.Last().Uuid);
                external.RemoveScene(sim.Snapshot().Scenes.Last().Uuid);
            }).Message);
            external.RemoveItem(scene, item);
            sim.CheckInvariants();
        }

        [Fact]
        public void TheRangesOfChangesOfSomeoneElse_AreThoseOfTheRequestsOfTheSameName()
        {
            // SetSceneItemIndex takes an index from 0 to 8192 and SetInputVolume a volume from -100 to 26 dB. A double that is not a number is in no range
            // (JSON cannot carry it, but a test can hand it over). The refusals leave the world as it was; the limits themselves are in range.
            var sim = new SimObs(Ideal);
            var external = sim.External;
            var scene = external.CreateScene("S");
            var (input, item) = external.CreateInput(scene, "Mic", "wasapi_input_capture");
            var before = sim.Snapshot().ToString();

            Assert.Contains("402", Assert.Throws<InvalidOperationException>(() => external.SetItemIndex(scene, item, -1)).Message);
            Assert.Contains("402", Assert.Throws<InvalidOperationException>(() => external.SetItemIndex(scene, item, 8193)).Message);
            Assert.Contains("402", Assert.Throws<InvalidOperationException>(() => external.SetVolumeDb(input, -100.5)).Message);
            Assert.Contains("402", Assert.Throws<InvalidOperationException>(() => external.SetVolumeDb(input, 26.5)).Message);
            Assert.Contains("402", Assert.Throws<InvalidOperationException>(() => external.SetVolumeDb(input, double.NaN)).Message);
            Assert.Contains("402", Assert.Throws<InvalidOperationException>(() => external.SetVolumeDb(input, double.PositiveInfinity)).Message);
            Assert.Equal(before, sim.Snapshot().ToString());

            external.SetItemIndex(scene, item, 8192);
            external.SetItemIndex(scene, item, 0);
            external.SetVolumeDb(input, -100);
            external.SetVolumeDb(input, 26);
            sim.CheckInvariants();
        }

        [Fact]
        public void AChangeOfSomeoneElseThatHangsInOBS_IsRefused()
        {
            var sim = new SimObs(Ideal with { HangOnRemovedSceneRename = true });
            var external = sim.External;
            var parent = external.CreateScene("P");
            var child = external.CreateScene("C");
            external.AddItem(parent, child);
            external.RemoveScene(child);

            Assert.Contains("never answers", Assert.Throws<InvalidOperationException>(() => external.RenameScene(child, "X")).Message);
        }

        [Fact]
        public void ASnapshotIsReadableAndComparesDeeply()
        {
            var one = new SimHarness(Ideal, seed: 3);
            var two = new SimHarness(Ideal, seed: 3);
            var three = new SimHarness(Ideal, seed: 4);
            foreach (var h in new[] { one, two, three })
            {
                var scene = h.CreateScene("S");
                h.CreateInput(scene, "Mic", "wasapi_input_capture");
                h.Ok("SetInputMute", new { inputName = "Mic", inputMuted = true });
            }

            var snapshot = one.Sim.Snapshot();

            Assert.Equal(snapshot, two.Sim.Snapshot());
            Assert.Equal(snapshot.GetHashCode(), two.Sim.Snapshot().GetHashCode());
            Assert.NotEqual(snapshot, three.Sim.Snapshot());
            Assert.NotEqual(snapshot, snapshot with { StudioMode = true });
            var text = snapshot.ToString();
            Assert.Contains("collection \"Untitled\"", text);
            Assert.Contains("\"Mic\" wasapi_input_capture", text);
            Assert.Contains("muted", text);
            Assert.Contains("#1 at 0: input \"Mic\"", text);
            Assert.Equal(snapshot.Scenes[0].Items, one.Sim.Snapshot().Scenes[0].Items);
        }
    }
}
