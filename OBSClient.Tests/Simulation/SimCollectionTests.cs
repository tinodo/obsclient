namespace OBSStudioClient.Tests.Simulation
{
    using System.Text.Json;

    /// <summary>
    /// The scene collections of the simulated OBS Studio: the list, the switch (the two events, the time in between in which obs-websocket is not ready, and the
    /// response that waits for the end), what is saved and loaded, and what the quirks add. What the simulation does here was read from the source of
    /// obs-websocket (EventHandler.cpp, WebSocketServer_Protocol.cpp, RequestHandler_Config.cpp) and not yet measured on a running OBS Studio.
    /// </summary>
    public class SimCollectionTests
    {
        private const string NotReady = "OBS is not ready to perform the request.";

        // Every switch takes half a second, and nothing else is slow, so that a test can say what is where at which time.
        private static readonly SimQuirks Slow = SimQuirks.Ideal with { CollectionSwitchTime = SimRange.Exactly(500) };

        [Fact]
        public void GetSceneCollectionList_ListsTheCollections_AlphabeticallyWithoutRegardToCase_AndTheCurrentOne()
        {
            var h = new SimHarness(Slow);

            var first = h.Ok("GetSceneCollectionList");
            h.Ok("CreateSceneCollection", new { sceneCollectionName = "beta" });
            h.Ok("CreateSceneCollection", new { sceneCollectionName = "Alpha" });
            var later = h.Ok("GetSceneCollectionList");

            Assert.Equal("Untitled", first.String("currentSceneCollectionName"));
            Assert.Equal(["Untitled"], Names(first));
            Assert.Equal("Alpha", later.String("currentSceneCollectionName"));
            Assert.Equal(["Alpha", "beta", "Untitled"], Names(later));
            Assert.Equal(["Alpha", "beta", "Untitled"], h.Sim.Collections);
        }

        [Fact]
        public void ASwitchByRequest_AnnouncesChanging_ThenAnswersEveryRequestWith207_AndWhenItIsDone_AnswersAndAnnouncesChanged()
        {
            var h = new SimHarness(Slow);
            h.Ok("CreateSceneCollection", new { sceneCollectionName = "Second" });
            h.ClearLog();
            var started = h.Now;

            var switchId = h.Send("SetCurrentSceneCollection", new { sceneCollectionName = "Untitled" });
            h.Run(100);

            // The old collection is the one that is announced, and the server is not ready from then on.
            var changing = Assert.Single(h.Events("CurrentSceneCollectionChanging"));
            Assert.Equal("Second", changing.String("sceneCollectionName"));
            Assert.Equal(SimIntent.Config, changing.Intent);
            Assert.True(h.Sim.IsSwitching);
            Assert.Equal("Second", h.Sim.CurrentCollection);
            Assert.DoesNotContain(h.Log, entry => entry.RequestId == switchId);

            var refused = h.Call("GetSceneList");
            Assert.False(refused.Succeeded);
            Assert.Equal(SimStatus.NotReady, refused.Code);
            Assert.Equal(NotReady, refused.Comment);
            Assert.Equal(JsonValueKind.Undefined, refused.Data.ValueKind);

            var answer = h.AwaitResponse(switchId);
            h.Settle();

            // The request blocked until the end, and the new collection is announced with it.
            Assert.True(answer.Succeeded);
            Assert.Equal(started + TimeSpan.FromMilliseconds(500), answer.At);
            var changed = Assert.Single(h.Events("CurrentSceneCollectionChanged"));
            Assert.Equal("Untitled", changed.String("sceneCollectionName"));
            Assert.Equal(answer.At, changed.At);
            Assert.False(h.Sim.IsSwitching);
            Assert.Equal("Untitled", h.Sim.CurrentCollection);
            Assert.Equal("Untitled", h.Sim.Snapshot().CollectionName);
            Assert.True(h.Call("GetSceneList").Succeeded);
            h.Sim.CheckInvariants();
        }

        [Fact]
        public void ACollectionThatIsMade_HasOneScene_AndIsTheCurrentOne_AndTheOldOneIsAnnouncedAndTheNewOne()
        {
            var h = new SimHarness(Slow);
            var original = h.Sim.Snapshot();

            h.Ok("CreateSceneCollection", new { sceneCollectionName = "Second" });
            h.Settle();

            var snapshot = h.Sim.Snapshot();
            Assert.Equal("Second", snapshot.CollectionName);
            var scene = Assert.Single(snapshot.Scenes);
            Assert.Equal("Scene", scene.Name);
            Assert.NotEqual(original.Scenes[0].Uuid, scene.Uuid);
            Assert.Equal(scene.Uuid, snapshot.ProgramSceneUuid);
            Assert.Empty(snapshot.Inputs);
            Assert.Equal(["CurrentSceneCollectionChanging", "CurrentSceneCollectionChanged"], h.EventNames());
            Assert.Equal("Untitled", h.Events("CurrentSceneCollectionChanging")[0].String("sceneCollectionName"));
            Assert.Equal("Second", h.Events("CurrentSceneCollectionChanged")[0].String("sceneCollectionName"));
        }

        [Fact]
        public void WhenACollectionIsLoadedAgain_ItHasWhatWasSaved_WithTheSameUuids_AndNothingThatWasRemovedAndKept()
        {
            var h = new SimHarness(Slow);
            var gaming = h.CreateScene("Gaming");
            var (mic, micItem) = h.CreateInput(gaming, "Mic", "wasapi_input_capture");
            var (cam, _) = h.CreateInput(gaming, "Cam");
            h.Ok("SetInputMute", new { inputUuid = mic.ToString(), inputMuted = true });
            h.Ok("SetInputVolume", new { inputUuid = mic.ToString(), inputVolumeDb = -12.0 });
            h.Ok("SetSceneItemEnabled", new { sceneUuid = gaming.ToString(), sceneItemId = micItem, sceneItemEnabled = false });

            // A scene that is removed and that a scene item holds, with an input that only it uses, and an input that is removed and that a scene item holds.
            var nested = h.CreateScene("Nested");
            h.CreateInput(nested, "InNested");
            h.CreateSceneItem(gaming, nested);
            h.Ok("RemoveScene", new { sceneUuid = nested.ToString() });
            h.Ok("RemoveInput", new { inputUuid = cam.ToString() });
            var before = h.Sim.Snapshot();
            Assert.Single(before.DetachedScenes);
            Assert.Contains(before.Inputs, input => input.Removed);

            h.Ok("CreateSceneCollection", new { sceneCollectionName = "Other" });
            h.Ok("SetCurrentSceneCollection", new { sceneCollectionName = "Untitled" });
            h.Settle();

            var after = h.Sim.Snapshot();
            Assert.Equal("Untitled", after.CollectionName);
            Assert.Equal(before.Scenes.Select(scene => (scene.Uuid, scene.Name)), after.Scenes.Select(scene => (scene.Uuid, scene.Name)));
            Assert.Equal(before.ProgramSceneUuid, after.ProgramSceneUuid);
            Assert.Empty(after.DetachedScenes);

            // The input that was removed, the input that only the scene that was removed used, and the scene items that showed them or the scene, are gone.
            var micAfter = Assert.Single(after.Inputs);
            Assert.Equal(before.Inputs.Single(input => input.Uuid == mic), micAfter);
            Assert.True(micAfter.Muted);
            var loadedGaming = after.Scenes.Single(scene => scene.Uuid == gaming);
            var item = Assert.Single(loadedGaming.Items);
            Assert.Equal((micItem, 0, mic, false), (item.Id, item.Index, item.SourceUuid, item.Enabled));
            Assert.Equal(["Mic"], h.InputNames());
            h.Sim.CheckInvariants();
        }

        [Fact]
        public void APinnedInput_IsKeptWithTheCollection()
        {
            var h = new SimHarness(Slow);
            var desktop = h.Sim.External.CreateGlobalInput("Desktop Audio", "wasapi_output_capture");
            h.Run(1);
            h.Ok("CreateSceneCollection", new { sceneCollectionName = "Other" });
            Assert.Empty(h.Sim.Snapshot().Inputs);

            h.Ok("SetCurrentSceneCollection", new { sceneCollectionName = "Untitled" });

            var input = Assert.Single(h.Sim.Snapshot().Inputs);
            Assert.Equal((desktop, "Desktop Audio", true), (input.Uuid, input.Name, input.Pinned));
            h.Sim.CheckInvariants();
        }

        [Fact]
        public void SetCurrentSceneCollection_ForANameThatIsNotACollection_Is600WithoutAComment()
        {
            var h = new SimHarness(Slow);

            var response = h.Call("SetCurrentSceneCollection", new { sceneCollectionName = "Nothing" });

            Assert.Equal(SimStatus.ResourceNotFound, response.Code);
            Assert.Null(response.Comment);
            Assert.False(h.Sim.IsSwitching);
            Assert.Empty(h.Events());
        }

        [Fact]
        public void SetCurrentSceneCollection_ForTheCurrentCollection_SucceedsAndNothingHappens()
        {
            var h = new SimHarness(Slow);

            var response = h.Call("SetCurrentSceneCollection", new { sceneCollectionName = "Untitled" });
            h.Settle();

            Assert.True(response.Succeeded);
            Assert.Equal(TimeSpan.Zero, response.At);
            Assert.Empty(h.Events());
            Assert.False(h.Sim.IsSwitching);
        }

        [Fact]
        public void CreateSceneCollection_ForTheNameOfACollection_Is601WithoutAComment()
        {
            var h = new SimHarness(Slow);

            var response = h.Call("CreateSceneCollection", new { sceneCollectionName = "Untitled" });

            Assert.Equal(SimStatus.ResourceAlreadyExists, response.Code);
            Assert.Null(response.Comment);
            Assert.Empty(h.Events());
        }

        [Fact]
        public void TheRequestsOfTheCollections_CheckTheirData()
        {
            var h = new SimHarness(Slow);

            foreach (var type in new[] { "SetCurrentSceneCollection", "CreateSceneCollection" })
            {
                var none = h.Call(type);
                var missing = h.Call(type, new { other = 1 });
                var empty = h.Call(type, new { sceneCollectionName = string.Empty });
                var number = h.Call(type, new { sceneCollectionName = 42 });

                Assert.Equal(SimStatus.MissingRequestData, none.Code);
                Assert.Equal((SimStatus.MissingRequestField, "Your request is missing the `sceneCollectionName` field."), (missing.Code, missing.Comment));
                Assert.Equal((SimStatus.RequestFieldEmpty, "The field value of `sceneCollectionName` must not be empty."), (empty.Code, empty.Comment));
                Assert.Equal((SimStatus.InvalidRequestFieldType, "The field value of `sceneCollectionName` must be a string."), (number.Code, number.Comment));
            }

            Assert.Empty(h.Events());
        }

        [Fact]
        public void ABatchThatIsSentWhileTheServerIsNotReady_Gets207ForEveryRequest_OrForTheFirstOneWhenItHalts()
        {
            var h = new SimHarness(Slow);
            h.Ok("CreateSceneCollection", new { sceneCollectionName = "Second" });
            h.Send("SetCurrentSceneCollection", new { sceneCollectionName = "Untitled" });
            h.Run(100);
            object[] requests = [new { requestType = "GetSceneList" }, new { requestType = "GetInputList", requestId = "second" }, new { requestType = "NoSuchRequest" }];

            var all = h.CallBatch(requests);
            var halting = h.CallBatch(requests, haltOnFailure: true);
            var parallel = h.CallBatch(requests, executionType: 2);

            var results = all.Data.EnumerateArray().ToArray();
            Assert.Equal(["GetSceneList", "GetInputList", "NoSuchRequest"], results.Select(result => result.GetProperty("requestType").GetString()));
            Assert.All(results, result =>
            {
                Assert.Equal(SimStatus.NotReady, result.GetProperty("requestStatus").GetProperty("code").GetInt32());
                Assert.Equal(NotReady, result.GetProperty("requestStatus").GetProperty("comment").GetString());
                Assert.False(result.TryGetProperty("responseData", out _));
            });
            Assert.Equal("second", results[1].GetProperty("requestId").GetString());
            Assert.False(results[0].TryGetProperty("requestId", out _));
            Assert.Equal(["GetSceneList"], halting.Data.EnumerateArray().Select(result => result.GetProperty("requestType").GetString()));
            Assert.Equal(3, parallel.Data.GetArrayLength());
            Assert.True(h.Sim.IsSwitching);
        }

        [Fact]
        public void ABatchThatIsSentWhileTheServerIsNotReady_GetsTheRequestTypeBackAsItCame()
        {
            // obs-websocket writes the request type of a result back as the JSON that came: the empty string that stands for a type that is not a string is made only
            // when the server is ready.
            var h = new SimHarness(Slow);
            h.Ok("CreateSceneCollection", new { sceneCollectionName = "Second" });
            h.Send("SetCurrentSceneCollection", new { sceneCollectionName = "Untitled" });
            h.Run(100);

            var response = h.CallBatch([new { requestType = 5 }, new { requestType = (string?)null, requestId = (string?)null }, new { requestId = "x" }, new { requestType = new[] { 1 }, requestId = 7 }]);

            var results = response.Data.EnumerateArray().ToArray();
            Assert.Equal(JsonValueKind.Number, results[0].GetProperty("requestType").ValueKind);
            Assert.Equal(5, results[0].GetProperty("requestType").GetInt32());
            Assert.Equal(JsonValueKind.Null, results[1].GetProperty("requestType").ValueKind);
            Assert.False(results[1].TryGetProperty("requestId", out _));
            Assert.Equal(JsonValueKind.Null, results[2].GetProperty("requestType").ValueKind);
            Assert.Equal("x", results[2].GetProperty("requestId").GetString());
            Assert.Equal(JsonValueKind.Array, results[3].GetProperty("requestType").ValueKind);
            Assert.Equal(7, results[3].GetProperty("requestId").GetInt32());
            Assert.All(results, result => Assert.Equal(SimStatus.NotReady, result.GetProperty("requestStatus").GetProperty("code").GetInt32()));
        }

        [Fact]
        public void ARequestThatWasSentBeforeTheSwitch_ButRunsInTheMiddleOfIt_Gets207()
        {
            // A client that has requests in flight when the collection begins to change: they find the server not ready, as the ones that come later.
            var h = new SimHarness(Slow);
            h.Ok("CreateSceneCollection", new { sceneCollectionName = "Second" });
            var switchId = h.Send("SetCurrentSceneCollection", new { sceneCollectionName = "Untitled" });
            var inFlight = h.Send("GetSceneList");

            h.Settle();

            var response = h.Log.Single(entry => entry.RequestId == inFlight);
            Assert.Equal(SimStatus.NotReady, response.Code);
            Assert.True(h.Log.Single(entry => entry.RequestId == switchId).Succeeded);
        }

        [Fact]
        public void WhileTheCollectionChanges_NobodyCanChangeAnything_ButTheRequestsAreAnsweredAndNothingIsSent()
        {
            var h = new SimHarness(Slow);
            h.Sim.External.CreateCollection("Second");
            h.Run(100);
            h.ClearLog();

            var refusal = Assert.Throws<InvalidOperationException>(() => h.Sim.External.CreateScene("Gaming"));
            var again = Assert.Throws<InvalidOperationException>(() => h.Sim.External.SwitchCollection("Untitled"));
            var create = h.Call("CreateScene", new { sceneName = "Gaming" });
            h.Settle();

            Assert.Equal(SimStatus.NotReady, Assert.IsType<SimFailure>(refusal.InnerException).Code);
            Assert.Equal(SimStatus.NotReady, Assert.IsType<SimFailure>(again.InnerException).Code);
            Assert.Equal(SimStatus.NotReady, create.Code);
            Assert.Equal(["CurrentSceneCollectionChanged"], h.EventNames());
            Assert.DoesNotContain(h.Sim.Snapshot().Scenes, scene => scene.Name == "Gaming");
            Assert.Equal("Second", h.Sim.Snapshot().CollectionName);
        }

        [Fact]
        public void ASwitchOfTheUser_IsAnnouncedWithTheTwoEvents_AndNothingIsAnswered()
        {
            var h = new SimHarness(Slow);

            h.Sim.External.CreateCollection("Second");
            h.Settle();
            h.Sim.External.SwitchCollection("Untitled");
            h.Settle();
            h.Sim.External.SwitchCollection("Untitled");
            h.Settle();

            Assert.Equal(
                [
                    ("CurrentSceneCollectionChanging", "Untitled"),
                    ("CurrentSceneCollectionChanged", "Second"),
                    ("CurrentSceneCollectionChanging", "Second"),
                    ("CurrentSceneCollectionChanged", "Untitled"),
                ],
                h.Events().Select(entry => (entry.Name, entry.String("sceneCollectionName"))));
            Assert.DoesNotContain(h.Log, entry => !entry.IsEvent);
            Assert.Equal("Untitled", h.Sim.CurrentCollection);
        }

        [Fact]
        public void TheUserCannotSwitchToACollectionThatIsNotThere_OrMakeOneThatIs()
        {
            var h = new SimHarness(Slow);

            var missing = Assert.Throws<InvalidOperationException>(() => h.Sim.External.SwitchCollection("Nothing"));
            var exists = Assert.Throws<InvalidOperationException>(() => h.Sim.External.CreateCollection("Untitled"));

            Assert.Equal(SimStatus.ResourceNotFound, Assert.IsType<SimFailure>(missing.InnerException).Code);
            Assert.Equal(SimStatus.ResourceAlreadyExists, Assert.IsType<SimFailure>(exists.InnerException).Code);
            Assert.Empty(h.Events());
            Assert.False(h.Sim.IsSwitching);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(500)]
        public void WhatTheCollectionThatWasLeftHadScheduled_DoesNotHappen(int switchMilliseconds)
        {
            // A transition of 300 ms is running when the user switches: whether the switch is done before it would end (0) or after (500), it never ends.
            var quirks = SimQuirks.Ideal with { CollectionSwitchTime = SimRange.Exactly(switchMilliseconds), TransitionDuration = TimeSpan.FromMilliseconds(300) };
            var h = new SimHarness(quirks);
            var gaming = h.CreateScene("Gaming");
            h.Ok("SetCurrentProgramScene", new { sceneUuid = gaming.ToString() });
            Assert.Contains("SceneTransitionStarted", h.EventNames());

            h.Sim.External.CreateCollection("Second");
            h.Settle();

            Assert.DoesNotContain("SceneTransitionEnded", h.EventNames());
            Assert.DoesNotContain("SceneTransitionVideoEnded", h.EventNames());
            Assert.DoesNotContain("CurrentProgramSceneChanged", h.EventNames());
            Assert.Equal("CurrentSceneCollectionChanged", h.EventNames()[^1]);
            h.Sim.CheckInvariants();
        }

        [Fact]
        public void TheSwitchTakesTheTimeOfTheQuirk_AndNoTimeWithoutIt()
        {
            var slow = new SimHarness(Slow);
            var instant = new SimHarness(SimQuirks.Ideal);

            slow.Sim.External.CreateCollection("Second");
            slow.Settle();
            instant.Sim.External.CreateCollection("Second");
            instant.Settle();

            Assert.Equal(TimeSpan.FromMilliseconds(500), slow.Events("CurrentSceneCollectionChanged")[0].At - slow.Events("CurrentSceneCollectionChanging")[0].At);
            Assert.Equal(TimeSpan.Zero, instant.Events("CurrentSceneCollectionChanged")[0].At - instant.Events("CurrentSceneCollectionChanging")[0].At);
        }

        [Fact]
        public void TheCreationEventsOfTheLoad_AreSentBetweenTheTwoEvents_OnlyWhenTheQuirkIsOn()
        {
            var on = new SimHarness(Slow with { CreationEventsDuringSwitch = true });
            var off = new SimHarness(Slow);
            foreach (var h in new[] { on, off })
            {
                var gaming = h.CreateScene("Gaming");
                h.CreateInput(gaming, "Mic", "wasapi_input_capture");
                h.Ok("CreateSceneCollection", new { sceneCollectionName = "Other" });
                h.ClearLog();
                h.Ok("SetCurrentSceneCollection", new { sceneCollectionName = "Untitled" });
                h.Settle();
            }

            Assert.Equal(["CurrentSceneCollectionChanging", "CurrentSceneCollectionChanged"], off.EventNames());
            Assert.Equal(
                [
                    "CurrentSceneCollectionChanging",
                    "InputCreated",
                    "SceneCreated",
                    "SceneCreated",
                    "SceneItemCreated",
                    "SceneListChanged",
                    "CurrentSceneCollectionChanged",
                ],
                on.EventNames());
            on.Sim.CheckInvariants();
        }

        [Fact]
        public void ASwitchInABatch_IsDoneAtOnce_AndTheRestOfTheBatchRunsInTheNewCollection()
        {
            var h = new SimHarness(Slow);
            h.CreateScene("Gaming");
            h.Ok("CreateSceneCollection", new { sceneCollectionName = "Second" });
            h.ClearLog();

            var batch = h.CallBatch(
            [
                new { requestType = "SetCurrentSceneCollection", requestData = new { sceneCollectionName = "Untitled" } },
                new { requestType = "GetSceneList" },
                new { requestType = "CreateSceneCollection", requestData = new { sceneCollectionName = "Third" } },
                new { requestType = "CreateScene", requestData = new { sceneName = "InThird" } },
            ]);
            h.Settle();

            var results = batch.Data.EnumerateArray().ToArray();
            Assert.All(results, result => Assert.Equal(SimStatus.Success, result.GetProperty("requestStatus").GetProperty("code").GetInt32()));
            Assert.Equal(
                ["Gaming", "Scene"],
                results[1].GetProperty("responseData").GetProperty("scenes").EnumerateArray().Select(scene => scene.GetProperty("sceneName").GetString()).Order());
            Assert.Equal("Third", h.Sim.CurrentCollection);
            Assert.Contains(h.Sim.Snapshot().Scenes, scene => scene.Name == "InThird");
            Assert.Equal(
                ["CurrentSceneCollectionChanging", "CurrentSceneCollectionChanged", "CurrentSceneCollectionChanging", "CurrentSceneCollectionChanged"],
                h.EventNames().Where(name => name.StartsWith("CurrentSceneCollection", StringComparison.Ordinal)));
            Assert.False(h.Sim.IsSwitching);
            h.Sim.CheckInvariants();
        }

        [Fact]
        public void TheSameSeed_GivesTheSameSwitches_AndAnotherSeedAnotherTime()
        {
            static string Run(int seed)
            {
                var h = new SimHarness(SimQuirks.Hostile, seed);
                h.CreateScene("Gaming");
                h.Ok("CreateSceneCollection", new { sceneCollectionName = "Second" });
                h.Ok("SetCurrentSceneCollection", new { sceneCollectionName = "Untitled" });
                h.Settle();
                return string.Join('\n', h.Log.Select(entry => entry.Message.SentAt.Ticks + " " + entry.Message.Json));
            }

            Assert.Equal(Run(7), Run(7));
            Assert.NotEqual(Run(7), Run(8));
        }

        private static string[] Names(SimEntry response)
        {
            return response.Data.GetProperty("sceneCollections").EnumerateArray().Select(name => name.GetString()!).ToArray();
        }
    }
}
