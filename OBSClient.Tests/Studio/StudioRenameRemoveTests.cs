namespace OBSStudioClient.Tests.Studio
{
    using System.Text.Json;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Studio;
    using OBSStudioClient.Tests.TestSupport;

    /// <summary>
    /// The changes that give scenes and inputs another name, remove them (with the scene items that keep them alive), and show or hide an input in every scene, against a stand-in
    /// for OBS Studio that answers what a test tells it to: the requests, in their order, what the model shows when the call returns, and what the exceptions say when OBS Studio
    /// refuses. (With the simulation of OBS Studio: <see cref="StudioMakeOverSimulationTests"/>.)
    /// </summary>
    public class StudioRenameRemoveTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private delegate (int Code, object? Data)? Handler(string type, JsonElement? data);

        private static Guid Uuid(JsonElement? data, string property)
        {
            return data!.Value.GetProperty(property).GetGuid();
        }

        private static int Number(JsonElement? data, string property)
        {
            return data!.Value.GetProperty(property).GetInt32();
        }

        // OBS Studio removes what it is asked to remove, as it does (what nothing shows is destroyed, what is shown stays alive), and answers. A test can answer some requests itself.
        private static Func<string, JsonElement?, (int Code, object? Data)?> ObsRemoves(ByNameFixture test, Handler? answerFirst = null)
        {
            return (type, data) =>
            {
                if (answerFirst?.Invoke(type, data) is { } answer)
                {
                    return answer;
                }

                switch (type)
                {
                    case "RemoveScene":
                        test.RemoveScene(Uuid(data, "sceneUuid"));
                        return (100, null);
                    case "RemoveInput":
                        test.RemoveInput(Uuid(data, "inputUuid"));
                        return (100, null);
                    case "RemoveSceneItem":
                        test.RemoveItem(Uuid(data, "sceneUuid"), Number(data, "sceneItemId"));
                        return (100, null);
                    default:
                        return null;
                }
            };
        }

        private static string[] Removals(ByNameFixture test)
        {
            return test.Responder.RequestLog.Where(type => type.StartsWith("Remove", StringComparison.Ordinal)).ToArray();
        }

        [Fact]
        public async Task AScene_IsRenamed_WhenTheModelShowsTheName()
        {
            await using var test = await ByNameFixture.StartAsync();
            var chatting = test.Studio.Scenes["Chatting"];
            string? asked = null;
            test.Responder.OnRequest = (type, data) =>
            {
                if (type != "SetSceneName")
                {
                    return null;
                }

                asked = data?.GetRawText();
                test.Rename(test.Chatting, "Talking");
                return (100, null);
            };

            await chatting.RenameAsync("Talking").WithTimeoutAsync(Timeout);

            Assert.Equal("Talking", chatting.Name);
            Assert.Same(chatting, test.Studio.Scenes["Talking"]);
            Assert.False(test.Studio.Scenes.Contains("Chatting"));
            Assert.Same(chatting, test.Studio.Scenes["Gaming"].Items["Talking"].Source);
            var request = JsonDocument.Parse(asked!).RootElement;
            Assert.Equal(test.Chatting, request.GetProperty("sceneUuid").GetGuid());
            Assert.Equal("Talking", request.GetProperty("newSceneName").GetString());
        }

        [Fact]
        public async Task AScene_WhoseNewNameIsTakenByAScene_IsANameInUseException_ThatNamesTheScene()
        {
            await using var test = await ByNameFixture.StartAsync();
            var chatting = test.Studio.Scenes["Chatting"];
            test.Responder.OnRequest = (type, data) => type == "SetSceneName" ? (601, null) : null;

            var exception = await Assert.ThrowsAsync<ObsNameInUseException>(() => chatting.RenameAsync("Gaming").WithTimeoutAsync(Timeout));

            Assert.Equal("Gaming", exception.Name);
            Assert.Same(test.Studio.Scenes["Gaming"], exception.HeldBy);
            Assert.Equal("OBS Studio refused renaming scene 'Chatting' to 'Gaming': a scene named 'Gaming' exists already. Nothing was changed.", exception.Message);
            Assert.Equal("Chatting", chatting.Name);
        }

        [Fact]
        public async Task AScene_RenamedToItsOwnName_IsANameInUseException_BecauseOBSStudioRefusesIt()
        {
            await using var test = await ByNameFixture.StartAsync();
            var gaming = test.Studio.Scenes["Gaming"];
            test.Responder.OnRequest = (type, data) => type == "SetSceneName" ? (601, null) : null;

            var exception = await Assert.ThrowsAsync<ObsNameInUseException>(() => gaming.RenameAsync("Gaming").WithTimeoutAsync(Timeout));

            Assert.Same(gaming, exception.HeldBy);
            Assert.Contains("the scene already has the name 'Gaming'", exception.Message);
        }

        [Fact]
        public async Task AScene_WhoseNewNameIsTakenByAnInput_IsANameInUseException_ThatNamesTheInput()
        {
            await using var test = await ByNameFixture.StartAsync();
            test.Responder.OnRequest = (type, data) => type == "SetSceneName" ? (601, null) : null;

            var exception = await Assert.ThrowsAsync<ObsNameInUseException>(() => test.Studio.Scenes["Chatting"].RenameAsync("Camera").WithTimeoutAsync(Timeout));

            Assert.Same(test.Studio.Inputs["Camera"], exception.HeldBy);
            Assert.Contains("an input named 'Camera' exists already, and this OBS Studio does not let a scene have the name of an input", exception.Message);
        }

        [Fact]
        public async Task AScene_WhoseNewNameIsTakenByASceneThatWasRemovedAndIsAlive_WithObsStudio31_0_IsANameInUseException_ThatSaysWhatKeepsItAlive()
        {
            // With OBS Studio 31.0 and older the name of a scene that was removed stays taken for as long as the scene is alive (obs-websocket 5.5 comes with it).
            await using var test = await ByNameFixture.StartAsync(webSocketVersion: "5.5.4", obsVersion: "31.0.0");
            var chatting = test.Studio.Scenes["Chatting"];
            test.RemoveScene(test.Chatting);
            await StudioRig.EventuallyAsync(() => chatting.State == ObsObjectState.Detached, "the scene to be detached");
            test.Responder.OnRequest = (type, data) => type == "SetSceneName" ? (601, null) : null;

            var exception = await Assert.ThrowsAsync<ObsNameInUseException>(() => test.Studio.Scenes["Spare"].RenameAsync("Chatting").WithTimeoutAsync(Timeout));

            Assert.Same(chatting, exception.HeldBy);
            Assert.Contains("a scene named 'Chatting' was removed, but OBS Studio keeps it alive because 1 scene item still shows it (scene item 4 of scene 'Gaming')", exception.Message);
            Assert.Contains("Call RemoveAsync() on the scene (HeldBy of this exception)", exception.Message);
        }

        [Theory]
        [InlineData("31.1.0", "5.6.2")]
        [InlineData("32.0.0", "5.6.3")]
        [InlineData("33.0.0", "5.7.6")]
        public async Task AScene_WhoseNewNameIsThatOfASceneThatWasRemovedAndIsAlive_WithObsStudio31_1OrNewer_IsNotBlamedOnThatScene(string obsVersion, string webSocketVersion)
        {
            // OBS Studio 31.1 and newer take the name of a scene that was removed out of the list of its canvas at once (obs_source_remove calls obs_canvas_remove_source), whatever version of
            // obs-websocket comes with it (32.0 has 5.6.3): whatever refused the name, it was not that scene.
            await using var test = await ByNameFixture.StartAsync(webSocketVersion: webSocketVersion, obsVersion: obsVersion);
            var chatting = test.Studio.Scenes["Chatting"];
            test.RemoveScene(test.Chatting);
            await StudioRig.EventuallyAsync(() => chatting.State == ObsObjectState.Detached, "the scene to be detached");
            test.Responder.OnRequest = (type, data) => type == "SetSceneName" ? (601, null) : null;

            var exception = await Assert.ThrowsAsync<ObsNameInUseException>(() => test.Studio.Scenes["Spare"].RenameAsync("Chatting").WithTimeoutAsync(Timeout));

            Assert.Null(exception.HeldBy);
            Assert.Contains("the model does not show which", exception.Message);
            Assert.DoesNotContain("keeps it alive", exception.Message);
        }

        [Fact]
        public async Task AReconnect_TakesTheRuleForTheNamesOfRemovedScenesFromTheObsStudioThatItReaches_AndKeepsNothingOfTheOneBefore()
        {
            await using var test = await ByNameFixture.StartAsync(webSocketVersion: "5.5.4", obsVersion: "31.0.0");
            Assert.True(test.Studio.RemovedScenesKeepTheirName);

            await ReconnectAsync(test, "5.7.6", "33.0.0");
            Assert.False(test.Studio.RemovedScenesKeepTheirName);

            await ReconnectAsync(test, "5.5.4", "31.0.0");
            Assert.True(test.Studio.RemovedScenesKeepTheirName);

            // A version that can not be read is no reason to keep what the connection before said: the newest behaviour.
            await ReconnectAsync(test, "5.7.6", "not a version");
            Assert.False(test.Studio.RemovedScenesKeepTheirName);
        }

        [Fact]
        public async Task AReconnect_AssumesTheNewestBehaviour_UntilTheNewConnectionHasSaidWhichObsStudioItIs()
        {
            await using var test = await ByNameFixture.StartAsync(webSocketVersion: "5.5.4", obsVersion: "31.0.0");
            Assert.True(test.Studio.RemovedScenesKeepTheirName);
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            test.Responder.VersionGate = gate;
            var handshakes = test.Responder.Handshakes;
            test.Responder.AbortConnection();

            // The new connection is made, and OBS Studio has not said its version yet: what the connection before said about its OBS Studio is not kept.
            await StudioRig.EventuallyAsync(() => test.Responder.Handshakes > handshakes && test.Studio.State == ObsStudioState.Synchronizing, "the new connection to be followed", TimeSpan.FromSeconds(30));
            Assert.False(test.Studio.RemovedScenesKeepTheirName);

            gate.SetResult();
            await StudioRig.EventuallyAsync(() => test.Studio.State == ObsStudioState.Synchronized, "the model to be read again", TimeSpan.FromSeconds(30));
            Assert.True(test.Studio.RemovedScenesKeepTheirName);
        }

        // The stand-in goes away and comes back as the OBS Studio of these versions (the client reconnects by itself, after a few seconds), and this returns when the model has read it.
        private static async Task ReconnectAsync(ByNameFixture test, string webSocketVersion, string obsVersion)
        {
            test.Responder.WebSocketVersion = webSocketVersion;
            test.Responder.ObsVersion = obsVersion;
            var handshakes = test.Responder.Handshakes;
            test.Responder.AbortConnection();
            await StudioRig.EventuallyAsync(() => test.Responder.Handshakes > handshakes && test.Studio.State == ObsStudioState.Synchronized, "the model to be read again from the new connection", TimeSpan.FromSeconds(30));
        }

        [Fact]
        public async Task AGroupThatWasRemovedAndIsAlive_HoldsItsName_AndTheMessageSaysToRemoveTheSceneItemThatShowsIt_NotTheGroup()
        {
            // A group is destroyed with the scene item that shows it, and RemoveAsync() refuses a group: the advice must not send the application there.
            await using var test = await ByNameFixture.StartAsync(withGroup: true, webSocketVersion: "5.5.4", obsVersion: "31.0.0");
            var group = (ObsScene)test.Studio.Scenes["Gaming"].Items["Group"].Source;
            await test.Responder.SendEventAsync("SceneRemoved", new { sceneName = "Group", sceneUuid = test.Group, isGroup = true }, 4);
            await StudioRig.EventuallyAsync(() => group.State == ObsObjectState.Detached, "the group to be detached");
            test.Responder.OnRequest = (type, data) => type == "SetInputName" ? (601, null) : null;

            var exception = await Assert.ThrowsAsync<ObsNameInUseException>(() => test.Studio.Inputs["Camera"].RenameAsync("Group").WithTimeoutAsync(Timeout));

            Assert.Same(group, exception.HeldBy);
            Assert.Contains("a group named 'Group' was removed, but OBS Studio keeps it alive because 1 scene item still shows it (scene item 5 of scene 'Gaming')", exception.Message);
            Assert.Contains("call RemoveAsync() on that scene item (scene item 5 of scene 'Gaming'), not on the group", exception.Message);
            Assert.DoesNotContain("Call RemoveAsync() on the group", exception.Message);
        }

        [Fact]
        public async Task AnInputThatAGroupShowsAndThatWasRemoved_HoldsItsName_AndTheMessageSaysToTakeItOutOfTheGroupFirst()
        {
            await using var test = await ByNameFixture.StartAsync(withGroup: true, inGroup: true);
            var logo = test.Studio.Inputs["Logo"];
            await test.Responder.SendEventAsync("InputRemoved", new { inputName = "Logo", inputUuid = logo.Uuid }, 8);
            await StudioRig.EventuallyAsync(() => logo.State == ObsObjectState.Detached, "the input to be detached");
            test.Responder.OnRequest = (type, data) => type == "SetInputName" ? (601, null) : null;

            var exception = await Assert.ThrowsAsync<ObsNameInUseException>(() => test.Studio.Inputs["Camera"].RenameAsync("Logo").WithTimeoutAsync(Timeout));

            Assert.Same(logo, exception.HeldBy);
            Assert.Contains("A scene item inside the group 'Group' shows it, and RemoveAsync() refuses an input that a group shows", exception.Message);
            Assert.Contains("take it out of the group in OBS Studio, and then call RemoveAsync()", exception.Message);
            Assert.DoesNotContain("Call RemoveAsync() on the input (HeldBy of this exception) to destroy it", exception.Message);
        }

        [Fact]
        public async Task AScene_IsNotRenamed_ToNothing_OrWhenItIsRemoved_OrWhenItIsAGroup()
        {
            await using var test = await ByNameFixture.StartAsync(withGroup: true);
            var chatting = test.Studio.Scenes["Chatting"];
            var group = (ObsScene)test.Studio.Scenes["Gaming"].Items["Group"].Source;
            test.RemoveScene(test.Chatting);
            await StudioRig.EventuallyAsync(() => chatting.State == ObsObjectState.Detached, "the scene to be detached");

            Assert.Throws<ArgumentException>(() => { _ = test.Studio.Scenes["Spare"].RenameAsync(string.Empty); });
            var removed = await Assert.ThrowsAsync<ObsRemovedException>(() => chatting.RenameAsync("Talking").WithTimeoutAsync(Timeout));
            var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => group.RenameAsync("Team").WithTimeoutAsync(Timeout));

            Assert.Equal(ObsObjectState.Detached, removed.State);
            Assert.Contains("The group 'Group' can not be renamed from here", refused.Message);
            Assert.Contains("Rename it in OBS Studio", refused.Message);
            Assert.DoesNotContain("ObsClient", refused.Message);
            Assert.Equal(0, test.Count("SetSceneName"));
        }

        [Fact]
        public async Task AnInput_IsRenamed_WhenTheModelShowsTheName()
        {
            await using var test = await ByNameFixture.StartAsync();
            var mic = test.Studio.Inputs["Mic"];
            string? asked = null;
            test.Responder.OnRequest = (type, data) =>
            {
                if (type != "SetInputName")
                {
                    return null;
                }

                asked = data?.GetRawText();
                test.Rename(test.Mic, "Microphone");
                return (100, null);
            };

            await mic.RenameAsync("Microphone").WithTimeoutAsync(Timeout);

            Assert.Equal("Microphone", mic.Name);
            Assert.Same(mic, test.Studio.Inputs["Microphone"]);
            Assert.False(test.Studio.Inputs.Contains("Mic"));
            Assert.Same(mic, test.Studio.Scenes["Chatting"].Items["Microphone"].Source);
            var request = JsonDocument.Parse(asked!).RootElement;
            Assert.Equal(test.Mic, request.GetProperty("inputUuid").GetGuid());
            Assert.Equal("Microphone", request.GetProperty("newInputName").GetString());
        }

        [Fact]
        public async Task AnInput_WhoseNewNameIsTakenByAnInput_IsANameInUseException_ThatNamesTheInput()
        {
            await using var test = await ByNameFixture.StartAsync();
            var mic = test.Studio.Inputs["Mic"];
            test.Responder.OnRequest = (type, data) => type == "SetInputName" ? (601, null) : null;

            var exception = await Assert.ThrowsAsync<ObsNameInUseException>(() => mic.RenameAsync("Camera").WithTimeoutAsync(Timeout));

            Assert.Same(test.Studio.Inputs["Camera"], exception.HeldBy);
            Assert.Equal("OBS Studio refused renaming input 'Mic' to 'Camera': an input named 'Camera' exists already. Nothing was changed.", exception.Message);
            Assert.Equal("Mic", mic.Name);
        }

        [Fact]
        public async Task AnInput_WhoseNewNameIsTakenByAScene_IsANameInUseException_ThatSaysThatAnInputCanNotHaveTheNameOfAScene()
        {
            await using var test = await ByNameFixture.StartAsync();
            test.Responder.OnRequest = (type, data) => type == "SetInputName" ? (601, null) : null;

            var exception = await Assert.ThrowsAsync<ObsNameInUseException>(() => test.Studio.Inputs["Mic"].RenameAsync("Gaming").WithTimeoutAsync(Timeout));

            Assert.Same(test.Studio.Scenes["Gaming"], exception.HeldBy);
            Assert.Contains("a scene named 'Gaming' exists already, and an input can not have the name of a scene", exception.Message);
        }

        [Fact]
        public async Task AnInput_RenamedToItsOwnName_IsANameInUseException_BecauseOBSStudioRefusesIt()
        {
            await using var test = await ByNameFixture.StartAsync();
            var mic = test.Studio.Inputs["Mic"];
            test.Responder.OnRequest = (type, data) => type == "SetInputName" ? (601, null) : null;

            var exception = await Assert.ThrowsAsync<ObsNameInUseException>(() => mic.RenameAsync("Mic").WithTimeoutAsync(Timeout));

            Assert.Same(mic, exception.HeldBy);
            Assert.Contains("the input already has the name 'Mic'", exception.Message);
        }

        [Fact]
        public async Task AnInput_WhoseNewNameIsTakenByAnInputThatWasRemovedAndIsAlive_IsANameInUseException_ThatSaysWhatKeepsItAlive()
        {
            await using var test = await ByNameFixture.StartAsync();
            await test.DetachAsync("Overlay", test.Overlay);
            test.Responder.OnRequest = (type, data) => type == "SetInputName" ? (601, null) : null;

            var exception = await Assert.ThrowsAsync<ObsNameInUseException>(() => test.Studio.Inputs["Camera"].RenameAsync("Overlay").WithTimeoutAsync(Timeout));

            Assert.Same(test.FindDetached(test.Overlay), exception.HeldBy);
            Assert.Contains("an input named 'Overlay' was removed, but OBS Studio keeps it alive because 2 scene items still show it", exception.Message);
        }

        [Fact]
        public async Task AnInput_IsNotRenamed_ToNothing_OrWhenItWasRemoved()
        {
            await using var test = await ByNameFixture.StartAsync();
            await test.DetachAsync("Overlay", test.Overlay);

            Assert.Throws<ArgumentException>(() => { _ = test.Studio.Inputs["Camera"].RenameAsync(string.Empty); });
            var removed = await Assert.ThrowsAsync<ObsRemovedException>(() => test.FindDetached(test.Overlay).RenameAsync("Logo").WithTimeoutAsync(Timeout));

            Assert.Equal(ObsObjectState.Detached, removed.State);
            Assert.Equal(0, test.Count("SetInputName"));
        }

        [Fact]
        public async Task AScene_ThatSceneItemsShow_IsRemovedWithThoseSceneItems_InOneBatchInThatOrder()
        {
            await using var test = await ByNameFixture.StartAsync();
            var chatting = test.Studio.Scenes["Chatting"];
            var gaming = test.Studio.Scenes["Gaming"];
            var asked = new List<string>();
            test.Responder.OnRequest = ObsRemoves(test, (type, data) =>
            {
                if (type.StartsWith("Remove", StringComparison.Ordinal))
                {
                    asked.Add(data!.Value.GetRawText());
                }

                return null;
            });

            await chatting.RemoveAsync().WithTimeoutAsync(Timeout);

            // The scene is removed first, and the scene item that showed it after it: that was the last thing that kept the scene alive, so it is gone.
            Assert.Equal(ObsObjectState.Gone, chatting.State);
            Assert.False(test.Studio.Scenes.Contains("Chatting"));
            Assert.Equal(["RemoveScene", "RemoveSceneItem"], Removals(test));

            // One batch: the requests are not sent one after the other (a batch stops at its first refusal; separate requests would go on).
            Assert.Single(test.Responder.BatchLog, batch => batch.SequenceEqual(["RemoveScene", "RemoveSceneItem"]));
            Assert.Equal(test.Chatting, JsonDocument.Parse(asked[0]).RootElement.GetProperty("sceneUuid").GetGuid());
            var item = JsonDocument.Parse(asked[1]).RootElement;
            Assert.Equal(test.Gaming, item.GetProperty("sceneUuid").GetGuid());
            Assert.Equal(4, item.GetProperty("sceneItemId").GetInt32());
            Assert.Equal(3, gaming.Items.Count);
            Assert.False(gaming.Items.TryGet("Chatting", out _));
        }

        [Fact]
        public async Task AScene_ThatNothingShows_IsRemoved_WithOneRequest()
        {
            await using var test = await ByNameFixture.StartAsync();
            var spare = test.Studio.Scenes["Spare"];
            test.Responder.OnRequest = ObsRemoves(test);

            await spare.RemoveAsync().WithTimeoutAsync(Timeout);

            Assert.Equal(ObsObjectState.Gone, spare.State);
            Assert.Equal(["RemoveScene"], Removals(test));
            Assert.Equal(3, test.Studio.Scenes.Count);
        }

        [Fact]
        public async Task AScene_WhoseRemovalOBSStudioRefuses_IsAResponseException_AndNoSceneItemIsRemoved()
        {
            await using var test = await ByNameFixture.StartAsync();
            var chatting = test.Studio.Scenes["Chatting"];

            // OBS Studio does not remove the last scene of a collection, and it stops the batch there.
            test.Responder.OnRequest = (type, data) => type == "RemoveScene" ? (603, null) : null;

            var exception = await Assert.ThrowsAsync<ObsResponseException>(() => chatting.RemoveAsync().WithTimeoutAsync(Timeout));

            Assert.Equal(RequestStatusCode.NotEnoughResources, exception.ErrorCode);
            Assert.Equal(["RemoveScene"], Removals(test));
            Assert.Equal(ObsObjectState.Live, chatting.State);
        }

        [Fact]
        public async Task AScene_WhoseSceneItemOBSStudioRefuses_IsAResponseException_ThatSaysWhatWasDone_AndRemovingItAgainRemovesWhatIsLeft()
        {
            await using var test = await ByNameFixture.StartAsync();
            var chatting = test.Studio.Scenes["Chatting"];
            var refuse = true;
            test.Responder.OnRequest = ObsRemoves(test, (type, data) =>
            {
                if (type == "RemoveSceneItem" && Volatile.Read(ref refuse))
                {
                    return (600, null);
                }

                return null;
            });

            var exception = await Assert.ThrowsAsync<ObsResponseException>(() => chatting.RemoveAsync().WithTimeoutAsync(Timeout));

            Assert.Equal(RequestStatusCode.ResourceNotFound, exception.ErrorCode);
            Assert.Contains("OBS Studio refused to remove scene item 4 of scene 'Gaming' while removing scene 'Chatting'", exception.Message);
            Assert.Contains("The requests before it were made, and the ones after it were not", exception.Message);
            Assert.Contains("Call RemoveAsync again to remove them", exception.Message);

            // The scene was removed, and the scene item keeps it alive: the model reads again, and says so.
            await StudioRig.EventuallyAsync(() => chatting.State == ObsObjectState.Detached, "the scene to be detached");
            Volatile.Write(ref refuse, false);
            await chatting.RemoveAsync().WithTimeoutAsync(Timeout);

            Assert.Equal(ObsObjectState.Gone, chatting.State);
            Assert.Equal(["RemoveScene", "RemoveSceneItem", "RemoveSceneItem"], Removals(test));
        }

        [Fact]
        public async Task AScene_ThatWasRemovedAndIsAlive_IsRemovedByRemovingItsSceneItems()
        {
            await using var test = await ByNameFixture.StartAsync();
            var chatting = test.Studio.Scenes["Chatting"];
            test.RemoveScene(test.Chatting);
            await StudioRig.EventuallyAsync(() => chatting.State == ObsObjectState.Detached, "the scene to be detached");
            test.Responder.OnRequest = ObsRemoves(test);

            await chatting.RemoveAsync().WithTimeoutAsync(Timeout);

            Assert.Equal(ObsObjectState.Gone, chatting.State);
            Assert.Equal(["RemoveSceneItem"], Removals(test));
        }

        [Fact]
        public async Task AScene_ThatIsGone_IsNotRemoved_AndAGroupIsRemovedThroughItsSceneItem()
        {
            await using var test = await ByNameFixture.StartAsync(withGroup: true);
            var spare = test.Studio.Scenes["Spare"];
            var group = (ObsScene)test.Studio.Scenes["Gaming"].Items["Group"].Source;
            test.RemoveScene(test.Spare);
            await StudioRig.EventuallyAsync(() => spare.State == ObsObjectState.Gone, "the scene to be gone");

            var gone = await Assert.ThrowsAsync<ObsRemovedException>(() => spare.RemoveAsync().WithTimeoutAsync(Timeout));
            var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => group.RemoveAsync().WithTimeoutAsync(Timeout));

            Assert.Equal(ObsObjectState.Gone, gone.State);
            Assert.Contains("The group 'Group' is removed by removing the scene item that shows it", refused.Message);
            Assert.Empty(Removals(test));

            // A group that is gone is gone, whatever it was: the call says that, as it does for a scene (the state is looked at before the kind). OBS Studio announces the removal of a group as it
            // does that of a scene (with isGroup), a moment before the scene item that showed it is announced as removed.
            test.RemoveItem(test.Gaming, 5);
            await test.Responder.SendEventAsync("SceneRemoved", new { sceneName = "Group", sceneUuid = test.Group, isGroup = true }, 4);
            await StudioRig.EventuallyAsync(() => group.State == ObsObjectState.Gone, "the group to be gone");
            var goneGroup = await Assert.ThrowsAsync<ObsRemovedException>(() => group.RemoveAsync().WithTimeoutAsync(Timeout));
            Assert.Equal(ObsObjectState.Gone, goneGroup.State);
        }

        [Fact]
        public async Task AGroupThatIsGone_IsCalledAGroupInTheMessagesAboutIt_AndASceneIsCalledAScene()
        {
            // A group is a scene of OBS Studio, and an application calls it a group: the message of an exception about a group that was removed says so.
            await using var test = await ByNameFixture.StartAsync(withGroup: true);
            var gaming = test.Studio.Scenes["Gaming"];
            var spare = test.Studio.Scenes["Spare"];
            var group = (ObsScene)gaming.Items["Group"].Source;
            test.RemoveItem(test.Gaming, 5);
            await test.Responder.SendEventAsync("SceneRemoved", new { sceneName = "Group", sceneUuid = test.Group, isGroup = true }, 4);
            await StudioRig.EventuallyAsync(() => group.State == ObsObjectState.Gone, "the group to be gone");
            test.RemoveScene(test.Spare);
            await StudioRig.EventuallyAsync(() => spare.State == ObsObjectState.Gone, "the scene to be gone");

            var removed = await Assert.ThrowsAsync<ObsRemovedException>(() => group.RemoveAsync().WithTimeoutAsync(Timeout));
            var renamed = await Assert.ThrowsAsync<ObsRemovedException>(() => group.RenameAsync("Team").WithTimeoutAsync(Timeout));
            var added = await Assert.ThrowsAsync<ObsRemovedException>(() => gaming.AddAsync(group).WithTimeoutAsync(Timeout));
            var sceneRemoved = await Assert.ThrowsAsync<ObsRemovedException>(() => spare.RemoveAsync().WithTimeoutAsync(Timeout));
            var sceneAdded = await Assert.ThrowsAsync<ObsRemovedException>(() => gaming.AddAsync(spare).WithTimeoutAsync(Timeout));

            Assert.All(new[] { removed, renamed, added }, exception =>
            {
                Assert.Contains("group 'Group'", exception.Message);
                Assert.DoesNotContain("scene 'Group'", exception.Message);
            });
            Assert.Contains("scene 'Spare'", sceneRemoved.Message);
            Assert.Contains("scene 'Spare'", sceneAdded.Message);
        }

        [Fact]
        public async Task AnInput_ThatSceneItemsShow_IsRemovedWithThoseSceneItems_InOneBatchInThatOrder()
        {
            await using var test = await ByNameFixture.StartAsync();
            var mic = test.Studio.Inputs["Mic"];
            var asked = new List<string>();
            test.Responder.OnRequest = ObsRemoves(test, (type, data) =>
            {
                if (type.StartsWith("Remove", StringComparison.Ordinal))
                {
                    asked.Add(type + " " + data!.Value.GetRawText());
                }

                return null;
            });

            await mic.RemoveAsync().WithTimeoutAsync(Timeout);

            // The input is removed first, and the scene items that showed it after it, in the scenes that showed it: the last one destroys the input.
            Assert.Equal(ObsObjectState.Gone, mic.State);
            Assert.False(test.Studio.Inputs.Contains("Mic"));
            Assert.Equal(["RemoveInput", "RemoveSceneItem", "RemoveSceneItem"], Removals(test));

            // One batch: the requests are not sent one after the other (a batch stops at its first refusal; separate requests would go on).
            Assert.Single(test.Responder.BatchLog, batch => batch.SequenceEqual(["RemoveInput", "RemoveSceneItem", "RemoveSceneItem"]));
            Assert.Contains(test.Mic.ToString(), asked[0]);
            Assert.Single(asked, entry => entry.Contains($"\"sceneUuid\":\"{test.Gaming}\"", StringComparison.Ordinal) && entry.Contains("\"sceneItemId\":2", StringComparison.Ordinal));
            Assert.Single(asked, entry => entry.Contains($"\"sceneUuid\":\"{test.Chatting}\"", StringComparison.Ordinal) && entry.Contains("\"sceneItemId\":1", StringComparison.Ordinal));
            Assert.False(test.Studio.Scenes["Gaming"].Items.TryGet("Mic", out _));
            Assert.False(test.Studio.Scenes["Chatting"].Items.TryGet("Mic", out _));
        }

        [Fact]
        public async Task AnInput_ThatNoSceneItemShows_IsRemoved_WithOneRequest()
        {
            await using var test = await ByNameFixture.StartAsync();
            var desktop = test.Studio.Inputs["Desktop Audio"];
            test.Responder.OnRequest = ObsRemoves(test);

            await desktop.RemoveAsync().WithTimeoutAsync(Timeout);

            Assert.Equal(ObsObjectState.Gone, desktop.State);
            Assert.Equal(["RemoveInput"], Removals(test));
        }

        [Fact]
        public async Task AnInput_WhoseRemovalOBSStudioRefuses_IsARemovedException_AndNoSceneItemIsRemoved()
        {
            await using var test = await ByNameFixture.StartAsync();
            var mic = test.Studio.Inputs["Mic"];
            test.Responder.OnRequest = (type, data) => type == "RemoveInput" ? (600, null) : null;

            var exception = await Assert.ThrowsAsync<ObsRemovedException>(() => mic.RemoveAsync().WithTimeoutAsync(Timeout));

            Assert.Equal("input 'Mic'", exception.Description);
            Assert.Equal(["RemoveInput"], Removals(test));
        }

        [Fact]
        public async Task AnInput_WhoseSceneItemOBSStudioRefuses_IsAResponseException_ThatSaysWhatWasDone_AndRemovingItAgainRemovesWhatIsLeft()
        {
            await using var test = await ByNameFixture.StartAsync();
            var mic = test.Studio.Inputs["Mic"];
            var refuse = 1;
            test.Responder.OnRequest = ObsRemoves(test, (type, data) =>
            {
                // The first scene item request that comes is refused: the scene item was removed meanwhile, says OBS Studio.
                if (type == "RemoveSceneItem" && Interlocked.Exchange(ref refuse, 0) == 1)
                {
                    return (600, null);
                }

                return null;
            });

            var exception = await Assert.ThrowsAsync<ObsResponseException>(() => mic.RemoveAsync().WithTimeoutAsync(Timeout));

            Assert.Equal(RequestStatusCode.ResourceNotFound, exception.ErrorCode);
            Assert.Contains("while removing input 'Mic'", exception.Message);
            Assert.Contains("The requests before it were made, and the ones after it were not", exception.Message);
            await StudioRig.EventuallyAsync(() => mic.State == ObsObjectState.Detached, "the input to be detached");

            await mic.RemoveAsync().WithTimeoutAsync(Timeout);

            Assert.Equal(ObsObjectState.Gone, mic.State);
            Assert.Equal(1, test.Count("RemoveInput"));
        }

        [Fact]
        public async Task AnInput_ThatWasRemovedAndIsAlive_IsRemovedByRemovingItsSceneItems()
        {
            await using var test = await ByNameFixture.StartAsync();
            await test.DetachAsync("Overlay", test.Overlay);
            var overlay = test.FindDetached(test.Overlay);
            test.Responder.OnRequest = ObsRemoves(test);

            await overlay.RemoveAsync().WithTimeoutAsync(Timeout);

            Assert.Equal(ObsObjectState.Gone, overlay.State);
            Assert.Equal(["RemoveSceneItem", "RemoveSceneItem"], Removals(test));
            Assert.False(test.Studio.Scenes["Gaming"].Items.TryGet("Overlay", out _));
            Assert.False(test.Studio.Scenes["Chatting"].Items.TryGet("Overlay", out _));
        }

        [Fact]
        public async Task AnInput_ThatIsGone_IsNotRemoved()
        {
            await using var test = await ByNameFixture.StartAsync();
            var desktop = test.Studio.Inputs["Desktop Audio"];
            test.RemoveInput(test.Desktop);
            await StudioRig.EventuallyAsync(() => desktop.State == ObsObjectState.Gone, "the input to be gone");

            var exception = await Assert.ThrowsAsync<ObsRemovedException>(() => desktop.RemoveAsync().WithTimeoutAsync(Timeout));

            Assert.Equal(ObsObjectState.Gone, exception.State);
            Assert.Empty(Removals(test));
        }

        [Fact]
        public async Task AnInput_ThatAGroupShows_IsNotRemoved_BecauseObsWebsocketRemovesNoSceneItemOfAGroup_AndNothingIsSent()
        {
            await using var test = await ByNameFixture.StartAsync(withGroup: true, inGroup: true);
            var logo = test.Studio.Inputs["Logo"];
            test.Responder.OnRequest = ObsRemoves(test);

            // RemoveSceneItem is for scenes only (obs-websocket 5.7.6 refuses the UUID of a group): the input would be removed and kept alive by the scene item of the group for good.
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => logo.RemoveAsync().WithTimeoutAsync(Timeout));

            Assert.Contains("The input 'Logo' is shown by scene item 1 of the group 'Group'", exception.Message);
            Assert.Contains("take it out of the group in OBS Studio first. Nothing was changed.", exception.Message);
            Assert.Equal(ObsObjectState.Live, logo.State);
            Assert.Empty(Removals(test));
        }

        [Fact]
        public async Task AScene_ThatAGroupShows_IsNotRemoved_BecauseObsWebsocketRemovesNoSceneItemOfAGroup_AndNothingIsSent()
        {
            await using var test = await ByNameFixture.StartAsync(withGroup: true, inGroup: true);
            var spare = test.Studio.Scenes["Spare"];
            test.Responder.OnRequest = ObsRemoves(test);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => spare.RemoveAsync().WithTimeoutAsync(Timeout));

            Assert.Contains("The scene 'Spare' is shown by scene item 2 of the group 'Group'", exception.Message);
            Assert.Equal(ObsObjectState.Live, spare.State);
            Assert.Empty(Removals(test));
        }

        [Fact]
        public async Task AnInput_WhoseSceneItemOBSStudioRefuses_ShowsWhatWasDone_WhenTheCallEnds_SoThatRemovingItAtOnceSendsOnlyWhatIsLeft()
        {
            await using var test = await ByNameFixture.StartAsync();
            var mic = test.Studio.Inputs["Mic"];
            var sceneItemRequests = 0;
            test.Responder.OnRequest = ObsRemoves(test, (type, data) =>
            {
                // The second scene item request is refused: the first one removed a scene item, which OBS Studio announces a moment after it answered.
                if (type == "RemoveSceneItem" && Interlocked.Increment(ref sceneItemRequests) == 2)
                {
                    return (600, null);
                }

                return null;
            });

            await Assert.ThrowsAsync<ObsResponseException>(() => mic.RemoveAsync().WithTimeoutAsync(Timeout));

            // The call ended with the model showing what the requests before the refusal made, so that a call made at once builds its requests from that, and does not repeat them.
            Assert.Equal(ObsObjectState.Detached, mic.State);
            Assert.Single(test.Studio.Scenes, scene => scene.Items.TryGet("Mic", out _));
            await mic.RemoveAsync().WithTimeoutAsync(Timeout);

            Assert.Equal(ObsObjectState.Gone, mic.State);
            Assert.Equal(["RemoveInput", "RemoveSceneItem", "RemoveSceneItem", "RemoveSceneItem"], Removals(test));
        }

        [Fact]
        public async Task AScene_WhoseSceneItemOBSStudioRefuses_ShowsWhatWasDone_WhenTheCallEnds_SoThatRemovingItAtOnceSendsOnlyWhatIsLeft()
        {
            await using var test = await ByNameFixture.StartAsync();
            var chatting = test.Studio.Scenes["Chatting"];
            var refuse = 1;
            test.Responder.OnRequest = ObsRemoves(test, (type, data) => type == "RemoveSceneItem" && Interlocked.Exchange(ref refuse, 0) == 1 ? (600, null) : null);

            await Assert.ThrowsAsync<ObsResponseException>(() => chatting.RemoveAsync().WithTimeoutAsync(Timeout));

            Assert.Equal(ObsObjectState.Detached, chatting.State);
            await chatting.RemoveAsync().WithTimeoutAsync(Timeout);

            Assert.Equal(ObsObjectState.Gone, chatting.State);
            Assert.Equal(["RemoveScene", "RemoveSceneItem", "RemoveSceneItem"], Removals(test));
        }

        [Fact]
        public async Task AnInput_WhoseSceneItemIsGoneWhenOBSStudioIsAskedToRemoveIt_EndsTheCallWhenTheModelShowsThatSceneItemGone_Too()
        {
            await using var test = await ByNameFixture.StartAsync();
            var mic = test.Studio.Inputs["Mic"];
            var sceneItemRequests = 0;
            test.Responder.OnRequest = ObsRemoves(test, (type, data) =>
            {
                if (type == "RemoveSceneItem" && Interlocked.Increment(ref sceneItemRequests) == 2)
                {
                    // The scene item was removed just before (by the user, or by OBS Studio itself): it is gone, no event has said so yet, and OBS Studio refuses the request.
                    var scene = Uuid(data, "sceneUuid");
                    var id = Number(data, "sceneItemId");
                    test.Responder.Change(world => world.Items[scene] = world.Items[scene].Where(item => item.Id != id).ToList());
                    return (600, null);
                }

                return null;
            });

            var refused = await Assert.ThrowsAsync<ObsResponseException>(() => mic.RemoveAsync().WithTimeoutAsync(Timeout));

            // The scene item that was refused is gone in OBS Studio, and the call does not end before the model shows that: a call that is made at once would send the request for it again.
            Assert.Equal(RequestStatusCode.ResourceNotFound, refused.ErrorCode);
            Assert.Equal(ObsObjectState.Detached, mic.State);
            Assert.Empty(mic.GetSceneItems());
        }

        [Fact]
        public async Task ADetachedInput_WhoseFirstSceneItemIsGoneWhenOBSStudioIsAskedToRemoveIt_EndsTheCallWhenTheModelShowsThatSceneItemGone_SoThatRemovingItAtOnceSendsOnlyWhatIsLeft()
        {
            await using var test = await ByNameFixture.StartAsync();
            await test.DetachAsync("Overlay", test.Overlay);
            var overlay = test.FindDetached(test.Overlay);
            var sceneItemRequests = 0;
            test.Responder.OnRequest = ObsRemoves(test, (type, data) =>
            {
                if (type == "RemoveSceneItem" && Interlocked.Increment(ref sceneItemRequests) == 1)
                {
                    // The scene item was removed just before (by the user, or by OBS Studio itself): it is gone, its event is on its way, and OBS Studio refuses the request. It is the first
                    // request of the call (the source is removed already), so no request of the call was made before it.
                    test.RemoveItem(Uuid(data, "sceneUuid"), Number(data, "sceneItemId"));
                    return (600, null);
                }

                return null;
            });

            var refused = await Assert.ThrowsAsync<ObsResponseException>(() => overlay.RemoveAsync().WithTimeoutAsync(Timeout));

            // The call does not end before the model shows that scene item gone: a call that is made at once would send the request for it again.
            Assert.Equal(RequestStatusCode.ResourceNotFound, refused.ErrorCode);
            Assert.Single(overlay.GetSceneItems());
            await overlay.RemoveAsync().WithTimeoutAsync(Timeout);

            Assert.Equal(ObsObjectState.Gone, overlay.State);
            Assert.Equal(["RemoveSceneItem", "RemoveSceneItem"], Removals(test));
        }

        [Fact]
        public async Task ADetachedScene_WhoseSceneItemIsGoneWhenOBSStudioIsAskedToRemoveIt_EndsTheCallWhenTheModelShowsThatSceneItemGone_AndTheSceneIsDestroyed()
        {
            await using var test = await ByNameFixture.StartAsync();
            var chatting = test.Studio.Scenes["Chatting"];
            test.RemoveScene(test.Chatting);
            await StudioRig.EventuallyAsync(() => chatting.State == ObsObjectState.Detached, "the scene to be detached");
            test.Responder.OnRequest = ObsRemoves(test, (type, data) =>
            {
                if (type != "RemoveSceneItem")
                {
                    return null;
                }

                // The only scene item that shows the scene is gone, and its event is on its way.
                test.RemoveItem(Uuid(data, "sceneUuid"), Number(data, "sceneItemId"));
                return (600, null);
            });

            var refused = await Assert.ThrowsAsync<ObsResponseException>(() => chatting.RemoveAsync().WithTimeoutAsync(Timeout));

            Assert.Equal(RequestStatusCode.ResourceNotFound, refused.ErrorCode);
            Assert.Equal(ObsObjectState.Gone, chatting.State);
            Assert.Equal(["RemoveSceneItem"], Removals(test));
        }

        [Fact]
        public async Task ASourceWhoseRemovalOBSStudioAnswersWithNotFound_EndsTheCallWhenTheModelShowsItRemoved_SoThatACallMadeAtOnceDoesNotAskAgain()
        {
            await using var test = await ByNameFixture.StartAsync();
            var mic = test.Studio.Inputs["Mic"];
            var chatting = test.Studio.Scenes["Chatting"];
            test.Responder.OnRequest = (type, data) =>
            {
                // The user removed the source just before: OBS Studio does not know it any more (it keeps it alive for the scene items that show it), and its event is on its way.
                switch (type)
                {
                    case "RemoveInput":
                        test.RemoveInput(Uuid(data, "inputUuid"));
                        return (600, null);
                    case "RemoveScene":
                        test.RemoveScene(Uuid(data, "sceneUuid"));
                        return (600, null);
                    default:
                        return null;
                }
            };

            await Assert.ThrowsAsync<ObsRemovedException>(() => mic.RemoveAsync().WithTimeoutAsync(Timeout));

            // The call says that the source is gone, and the model agrees by the time it says so.
            Assert.True(mic.IsRemoved);

            await Assert.ThrowsAsync<ObsRemovedException>(() => chatting.RemoveAsync().WithTimeoutAsync(Timeout));

            Assert.True(chatting.IsRemoved);
            Assert.Equal(["RemoveInput", "RemoveScene"], Removals(test));
        }

        [Fact]
        public async Task Reconciled_HasSomethingToWaitFor_OnlyWhenSomethingWasDoneOrIsGone()
        {
            await using var test = await ByNameFixture.StartAsync();
            var mic = test.Studio.Inputs["Mic"];
            var items = mic.GetSceneItems().ToArray();

            // The request that removes the source was refused, or the first scene item of a source that is removed already: nothing was done, and there is something to wait for only
            // when OBS Studio does not know it (600: it is gone).
            Assert.Null(StudioRemovals.Reconciled(mic, true, items, 0, RequestStatusCode.ResourceActionFailed));
            Assert.NotNull(StudioRemovals.Reconciled(mic, true, items, 0, RequestStatusCode.ResourceNotFound));
            Assert.Null(StudioRemovals.Reconciled(mic, false, items, 0, RequestStatusCode.ResourceActionFailed));
            Assert.NotNull(StudioRemovals.Reconciled(mic, false, items, 0, RequestStatusCode.ResourceNotFound));

            // A request after the first was refused: what the ones before it made is waited for, whatever the code.
            Assert.NotNull(StudioRemovals.Reconciled(mic, true, items, 1, RequestStatusCode.ResourceActionFailed));
            Assert.NotNull(StudioRemovals.Reconciled(mic, false, items, 1, RequestStatusCode.ResourceActionFailed));
        }

        [Fact]
        public async Task TheProgramScene_WhenItIsRemoved_ReturnsWhenTheModelShowsTheNewProgramScene_AlsoWhenTheFirstReadAfterTheRemovalStillSaysTheRemovedScene()
        {
            // A real OBS Studio makes another scene the program scene about a frame after it has answered the removal (16 ms were measured), and a read of the program scene in between still
            // says the scene that was removed: one read after the answer is not enough, and the call waits until the model shows the new program scene.
            await using var test = await ByNameFixture.StartAsync(new ObsStudioOptions { ConfirmationTimeout = TimeSpan.FromSeconds(5) });
            var program = test.Studio.ProgramScene!;
            var frameGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var frame = 0;
            var removed = 0;
            var staleReads = 0;
            test.Responder.OnRequest = ObsRemoves(test, (type, data) =>
            {
                switch (type)
                {
                    case "RemoveScene":
                        test.RemoveScene(Uuid(data, "sceneUuid"));
                        Volatile.Write(ref removed, 1);
                        _ = Task.Run(async () =>
                        {
                            // The frame: OBS Studio makes the first scene of its list the program scene, and says so (when the test lets it).
                            await frameGate.Task;
                            (Guid Uuid, string Name) next = default;
                            test.Responder.Change(world =>
                            {
                                next = (world.Scenes[0].Uuid, world.Scenes[0].Name);
                                world.Program = next.Uuid;
                            });
                            Volatile.Write(ref frame, 1);
                            await test.Responder.SendEventAsync("CurrentProgramSceneChanged", new { sceneName = next.Name, sceneUuid = next.Uuid }, 4);
                        });
                        return (100, null);
                    case "GetCurrentProgramScene" when Volatile.Read(ref frame) == 0:
                        if (Volatile.Read(ref removed) == 1)
                        {
                            Interlocked.Increment(ref staleReads);
                        }

                        return (100, new { sceneName = program.Name, sceneUuid = program.Uuid });
                    default:
                        return null;
                }
            });
            Assert.Equal("Scene", program.Name);

            var remove = program.RemoveAsync();
            await StudioRig.EventuallyAsync(() => program.IsRemoved && Volatile.Read(ref staleReads) > 0, "the scene to be removed in the model, and a read of the program scene after the removal to say the removed scene");

            // The scene is removed, and the program scene that the model has read is still the removed scene: the call has not returned.
            Assert.True(program.IsRemoved);
            Assert.True(program.IsProgram);
            Assert.False(remove.IsCompleted);

            // Now the frame comes.
            frameGate.SetResult();

            await remove.WithTimeoutAsync(Timeout);

            Assert.False(program.IsProgram);
            Assert.NotSame(program, test.Studio.ProgramScene);
            Assert.True(test.Studio.ProgramScene!.IsProgram);
        }

        [Fact]
        public async Task AnInputShownInSeveralScenes_WhoseSecondSceneItemOBSStudioRefuses_EndsTheCallWhenTheModelShowsWhatWasChanged_AndTheSceneItemThatIsGone()
        {
            await using var test = await ByNameFixture.StartAsync();
            var mic = test.Studio.Inputs["Mic"];
            Assert.Equal(2, mic.GetSceneItems().Count);
            var requests = 0;
            test.Responder.OnRequest = (type, data) =>
            {
                if (type != "SetSceneItemEnabled")
                {
                    return null;
                }

                var scene = Uuid(data, "sceneUuid");
                var id = Number(data, "sceneItemId");
                switch (Interlocked.Increment(ref requests))
                {
                    case 2:
                        // The second scene item was removed just before: it is gone, its event is on its way, and OBS Studio refuses the request.
                        test.RemoveItem(scene, id);
                        return (600, null);
                    default:
                        // The first one is changed, and nothing says so: the model learns of it by a read.
                        test.SetEnabled(scene, id, data!.Value.GetProperty("sceneItemEnabled").GetBoolean());
                        return (100, null);
                }
            };

            await Assert.ThrowsAsync<ObsRemovedException>(() => mic.SetEnabledInAllScenesAsync(false).WithTimeoutAsync(Timeout));

            // The call says that a scene item is gone, and the model shows that, and what the request before it changed, by the time that it says so.
            var left = mic.GetSceneItems();
            Assert.Single(left);
            Assert.False(left[0].IsEnabled);

            // A call that is made at once asks for the scene item that is left only.
            await mic.SetEnabledInAllScenesAsync(false).WithTimeoutAsync(Timeout);
            Assert.Equal(3, test.Count("SetSceneItemEnabled"));
        }

        [Fact]
        public async Task ASceneItem_AnInput_AndAScene_WhoseChangeOBSStudioAnswersWithNotFound_EndTheCallWhenTheModelShowsThemRemoved()
        {
            await using var test = await ByNameFixture.StartAsync();
            var item = test.Studio.Scenes["Gaming"].Items["Mic"];
            var mic = test.Studio.Inputs["Mic"];
            var chatting = test.Studio.Scenes["Chatting"];
            test.Responder.OnRequest = (type, data) =>
            {
                // Each was removed just before: OBS Studio does not know it any more, and the event is on its way.
                switch (type)
                {
                    case "SetSceneItemEnabled":
                        test.RemoveItem(Uuid(data, "sceneUuid"), Number(data, "sceneItemId"));
                        return (600, null);
                    case "SetInputMute":
                        test.RemoveInput(Uuid(data, "inputUuid"));
                        return (600, null);
                    case "SetSceneName":
                        test.RemoveScene(Uuid(data, "sceneUuid"));
                        return (600, null);
                    default:
                        return null;
                }
            };

            await Assert.ThrowsAsync<ObsRemovedException>(() => item.SetEnabledAsync(false).WithTimeoutAsync(Timeout));
            Assert.True(item.IsRemoved);
            await Assert.ThrowsAsync<ObsRemovedException>(() => mic.SetMutedAsync(true).WithTimeoutAsync(Timeout));
            Assert.True(mic.IsRemoved);
            await Assert.ThrowsAsync<ObsRemovedException>(() => chatting.RenameAsync("Talking").WithTimeoutAsync(Timeout));
            Assert.True(chatting.IsRemoved);

            // What was said is what the model showed: a call made at once is refused before anything is sent.
            await Assert.ThrowsAsync<ObsRemovedException>(() => item.SetEnabledAsync(true).WithTimeoutAsync(Timeout));
            Assert.Equal(1, test.Count("SetSceneItemEnabled"));
        }

        [Fact]
        public async Task AToggle_WhoseQuestionOBSStudioAnswersWithNotFound_EndsTheCallWhenTheModelShowsTheSceneItemRemoved()
        {
            await using var test = await ByNameFixture.StartAsync();
            var item = test.Studio.Scenes["Gaming"].Items["Mic"];
            test.Responder.OnRequest = (type, data) =>
            {
                if (type != "GetSceneItemEnabled")
                {
                    return null;
                }

                // The scene item was removed just before: OBS Studio does not know it any more, and the event is on its way.
                test.RemoveItem(Uuid(data, "sceneUuid"), Number(data, "sceneItemId"));
                return (600, null);
            };

            await Assert.ThrowsAsync<ObsRemovedException>(() => item.ToggleEnabledAsync().WithTimeoutAsync(Timeout));

            // The call says that the scene item is gone, and the model shows that by the time that it says so.
            Assert.True(item.IsRemoved);
        }

        [Fact]
        public async Task AnInputThatIsMadeInASceneThatOBSStudioDoesNotKnow_EndsTheCallWithoutAnEventThatRemovesTheScene_AndHasNoResultToReadPartsOf()
        {
            // The event that removes the scene is late or lost: the model goes on showing the scene, the wait for the events ends, and the model is read. A change that was refused has no
            // result, so the read names nothing of it (the audio of the input that was not made).
            await using var test = await ByNameFixture.StartAsync(new ObsStudioOptions { ConfirmationTimeout = TimeSpan.FromMilliseconds(300) });
            var gaming = test.Studio.Scenes["Gaming"];
            test.Responder.OnRequest = (type, data) => type == "CreateInput" ? (600, null) : null;

            await Assert.ThrowsAsync<ObsRemovedException>(() => gaming.CreateInputAsync("Extra", "color_source_v3").WithTimeoutAsync(Timeout));

            Assert.False(gaming.IsRemoved);
        }

        [Fact]
        public async Task ASceneAndAnInput_ThatOBSStudioLostWithoutAnEvent_AreTakenForRemoved_WhenAChangeAboutThemIsAnsweredWithNotFound()
        {
            // OBS Studio does not have them any more, and the model was never told (the event did not come). The change is refused with 600, no event says that they are gone, and the model
            // reads the lists, which do not have them: the call ends with them removed, and a call that is made right after is refused before it is sent.
            await using var test = await ByNameFixture.StartAsync(new ObsStudioOptions { ConfirmationTimeout = TimeSpan.FromMilliseconds(300) });
            var chatting = test.Studio.Scenes["Chatting"];
            var mic = test.Studio.Inputs["Mic"];
            test.Responder.OnRequest = (type, data) => type is "SetCurrentProgramScene" or "SetInputMute" ? (600, null) : null;
            test.Responder.Change(world =>
            {
                world.Scenes.RemoveAll(scene => scene.Uuid == chatting.Uuid);
                world.Items.Remove(chatting.Uuid);
                world.Inputs.RemoveAll(input => input.Uuid == mic.Uuid);
                foreach (var scene in world.Items.Keys.ToArray())
                {
                    world.Items[scene] = world.Items[scene].Where(item => item.Source != mic.Uuid).ToList();
                }
            });

            await Assert.ThrowsAsync<ObsRemovedException>(() => chatting.SwitchToAsync().WithTimeoutAsync(Timeout));
            Assert.True(chatting.IsRemoved);
            await Assert.ThrowsAsync<ObsRemovedException>(() => mic.SetMutedAsync(true).WithTimeoutAsync(Timeout));
            Assert.True(mic.IsRemoved);

            await Assert.ThrowsAsync<ObsRemovedException>(() => chatting.SwitchToAsync().WithTimeoutAsync(Timeout));
            Assert.Equal(1, test.Count("SetCurrentProgramScene"));
        }

        // OBS Studio still has the scene, and the list of scenes that it gives does not (the list is made from its user interface, which can be busy for seconds): no event says that the scene was
        // removed, and the model takes it for removed when it reads the list, which a scene that is made meanwhile makes it do.
        private static async Task LoseFromTheListAsync(ByNameFixture test, Guid uuid, ObsScene scene)
        {
            test.Responder.Change(world => world.Scenes.RemoveAll(listed => listed.Uuid == uuid));
            var made = test.MakeScene("Made while the list was busy");
            await test.Responder.SendEventAsync("SceneCreated", new { sceneName = "Made while the list was busy", sceneUuid = made, isGroup = false }, 4);
            await StudioRig.EventuallyAsync(() => scene.State == ObsObjectState.Detached, "the scene to be taken for removed");
        }

        [Fact]
        public async Task AScene_ThatTheListLostAndOBSStudioStillHas_IsAskedToBeRemoved_BecauseNoEventSaidThatItWasRemoved()
        {
            // The model took the scene for removed, and removing the scene items that show it (there are none) would leave it in OBS Studio while the call returns.
            await using var test = await ByNameFixture.StartAsync();
            var spare = test.Studio.Scenes["Spare"];
            await LoseFromTheListAsync(test, test.Spare, spare);
            var asked = new List<Guid>();
            test.Responder.OnRequest = (type, data) =>
            {
                if (type != "RemoveScene")
                {
                    return null;
                }

                asked.Add(Uuid(data, "sceneUuid"));
                return (100, null);
            };

            await spare.RemoveAsync().WithTimeoutAsync(Timeout);

            Assert.Equal([test.Spare], asked);
            Assert.Equal(["RemoveScene"], Removals(test));
        }

        [Fact]
        public async Task AScene_ThatTheListLostAndThatOBSStudioRemovedAndKeepsAliveForASceneItem_IsRemovedByItsSceneItems_WhenOBSStudioRefusesToRemoveItWith701()
        {
            // Real OBS Studio (33.0.0) refuses to remove a scene that it removed already and keeps alive: "Error getting canvas for the specified scene." (701). That is the answer for the scene items.
            await using var test = await ByNameFixture.StartAsync();
            var chatting = test.Studio.Scenes["Chatting"];
            await LoseFromTheListAsync(test, test.Chatting, chatting);
            test.Responder.OnRequest = ObsRemoves(test, (type, data) => type == "RemoveScene" ? (701, null) : null);

            await chatting.RemoveAsync().WithTimeoutAsync(Timeout);

            Assert.Equal(["RemoveScene", "RemoveSceneItem"], Removals(test));
        }

        [Fact]
        public async Task AScene_ThatTheListLostAndOBSStudioDoesNotHave_IsARemovedException_AndNoSceneItemIsRemoved()
        {
            await using var test = await ByNameFixture.StartAsync();
            var chatting = test.Studio.Scenes["Chatting"];
            await LoseFromTheListAsync(test, test.Chatting, chatting);
            test.Responder.OnRequest = ObsRemoves(test, (type, data) => type == "RemoveScene" ? (600, null) : null);

            var gone = await Assert.ThrowsAsync<ObsRemovedException>(() => chatting.RemoveAsync().WithTimeoutAsync(Timeout));

            Assert.Equal(ObsObjectState.Gone, gone.State);
            Assert.Equal(["RemoveScene"], Removals(test));
        }

        [Fact]
        public async Task AScene_ThatWasLiveWhenItsRemovalWasAskedFor_AndThatTheListLostWhileItWaitedForItsTurn_IsAskedToBeRemoved_AtItsTurn()
        {
            await using var test = await ByNameFixture.StartAsync();
            var spare = test.Studio.Scenes["Spare"];
            var camera = test.Studio.Scenes["Gaming"].Items["Camera"];
            var held = 0;
            var asked = new List<Guid>();
            test.Responder.OnRequest = (type, data) =>
            {
                switch (type)
                {
                    case "SetSceneItemEnabled":
                        // The first change is held, and the line waits for its answer.
                        test.SetEnabled(Uuid(data, "sceneUuid"), Number(data, "sceneItemId"), data!.Value.GetProperty("sceneItemEnabled").GetBoolean());
                        return Interlocked.Exchange(ref held, 1) == 0 ? (-1, null) : (100, null);
                    case "RemoveScene":
                        asked.Add(Uuid(data, "sceneUuid"));
                        return (100, null);
                    default:
                        return null;
                }
            };
            var change = camera.SetEnabledAsync(false);
            await StudioRig.EventuallyAsync(() => test.Responder.HeldCount == 1, "the change to be held");

            // The scene is live when its removal is asked for, and the model takes it for removed before the removal has its turn.
            var removal = spare.RemoveAsync();
            await LoseFromTheListAsync(test, test.Spare, spare);
            await test.Responder.AnswerHeldAsync();
            await change.WithTimeoutAsync(Timeout);
            await removal.WithTimeoutAsync(Timeout);

            Assert.Equal([test.Spare], asked);
        }

        [Fact]
        public async Task AScene_ThatTheListLostAndThatOBSStudioDestroysWhenItIsAskedToRemoveIt_IsRemoved_AlsoWhenTheModelShowsItGoneBeforeTheSceneItemsAreRemoved()
        {
            // The call has two steps: OBS Studio is asked to remove the scene, and then the scene items that show it are removed (none here). Another change is in front of the second step, and meanwhile OBS Studio
            // says that it destroyed the scene: the model shows it gone when the second step has its turn. The call is done then. It did not find a scene that somebody else had removed.
            await using var test = await ByNameFixture.StartAsync();
            var spare = test.Studio.Scenes["Spare"];
            var camera = test.Studio.Scenes["Gaming"].Items["Camera"];
            await LoseFromTheListAsync(test, test.Spare, spare);
            var held = 0;
            test.Responder.OnRequest = (type, data) =>
            {
                switch (type)
                {
                    case "RemoveScene":
                        return (100, null);
                    case "SetSceneItemEnabled":
                        test.SetEnabled(Uuid(data, "sceneUuid"), Number(data, "sceneItemId"), data!.Value.GetProperty("sceneItemEnabled").GetBoolean());
                        return Interlocked.Exchange(ref held, 1) == 0 ? (-1, null) : (100, null);
                    default:
                        return null;
                }
            };
            var removal = spare.RemoveAsync();
            var change = camera.SetEnabledAsync(false);
            await StudioRig.EventuallyAsync(() => test.Responder.HeldCount == 1, "the change to be held");
            await test.Responder.SendEventAsync("SceneRemoved", new { sceneName = "Spare", sceneUuid = test.Spare, isGroup = false }, 4);
            await StudioRig.EventuallyAsync(() => spare.State == ObsObjectState.Gone, "the scene to be gone");
            await test.Responder.AnswerHeldAsync();
            await change.WithTimeoutAsync(Timeout);

            await removal.WithTimeoutAsync(Timeout);

            Assert.Equal(["RemoveScene"], Removals(test));
        }

        [Fact]
        public async Task AnInput_ShownInASceneThatTheListLost_IsShownOrHiddenThereToo_BecauseOBSStudioCanStillHaveTheScene()
        {
            await using var test = await ByNameFixture.StartAsync();
            var mic = test.Studio.Inputs["Mic"];
            var chatting = test.Studio.Scenes["Chatting"];
            await LoseFromTheListAsync(test, test.Chatting, chatting);
            test.Responder.OnRequest = (type, data) =>
            {
                if (type != "SetSceneItemEnabled")
                {
                    return null;
                }

                test.SetEnabled(Uuid(data, "sceneUuid"), Number(data, "sceneItemId"), data!.Value.GetProperty("sceneItemEnabled").GetBoolean());
                return (100, null);
            };

            await mic.SetEnabledInAllScenesAsync(false).WithTimeoutAsync(Timeout);

            Assert.Equal(2, test.Count("SetSceneItemEnabled"));
            Assert.False(test.Studio.Scenes["Gaming"].Items["Mic"].IsEnabled);
            Assert.False(chatting.Items["Mic"].IsEnabled);
        }

        [Fact]
        public async Task AnInputThatTheUserRemovesWhileItsSceneItemsAreChanged_IsNotTheEndOfTheWait_BecauseItsSceneItemsAreAliveAndShown()
        {
            // OBS Studio keeps the removed input alive for its scene items, which are in live scenes and are changed: the call returns when the model shows that, not when it learns that the
            // input was removed (the scene items of a removed input are still shown, and the application reads them).
            await using var test = await ByNameFixture.StartAsync(new ObsStudioOptions { ConfirmationTimeout = TimeSpan.FromMilliseconds(300) });
            var mic = test.Studio.Inputs["Mic"];
            var removed = 0;
            test.Responder.OnRequest = (type, data) =>
            {
                if (type != "SetSceneItemEnabled")
                {
                    return null;
                }

                // The change is made and nothing says so: the model learns of it by a read. The user removes the input at the same time: OBS Studio keeps it alive, and says so.
                test.SetEnabled(Uuid(data, "sceneUuid"), Number(data, "sceneItemId"), data!.Value.GetProperty("sceneItemEnabled").GetBoolean());
                if (Interlocked.Exchange(ref removed, 1) == 0)
                {
                    test.RemoveInput(mic.Uuid);
                }

                return (100, null);
            };

            await mic.SetEnabledInAllScenesAsync(false).WithTimeoutAsync(Timeout);

            Assert.True(mic.IsRemoved);
            Assert.NotEmpty(mic.GetSceneItems());
            Assert.All(mic.GetSceneItems(), item => Assert.False(item.IsEnabled));
        }

        [Fact]
        public async Task ASceneWhoseRemovalOBSStudioAnswersWithNotFound_DoesNotWaitForTheProgramScene_BecauseNothingWasRemoved()
        {
            await using var test = await ByNameFixture.StartAsync();
            var chatting = test.Studio.Scenes["Chatting"];
            test.Responder.OnRequest = (type, data) =>
            {
                switch (type)
                {
                    case "RemoveScene":
                        test.RemoveScene(Uuid(data, "sceneUuid"));
                        return (600, null);
                    case "GetCurrentProgramScene":
                        // The program reads are held back, and never answered.
                        return (-1, null);
                    default:
                        return null;
                }
            };

            await Assert.ThrowsAsync<ObsRemovedException>(() => chatting.RemoveAsync().WithTimeoutAsync(Timeout));

            Assert.True(chatting.IsRemoved);
        }

        [Fact]
        public async Task AChangeToTheSceneItemsOfAnInput_ReadsTheScenesThatShowItWhenTheChangeHasItsTurn_NotThoseThatShowedItWhenItWasAskedFor()
        {
            await using var test = await ByNameFixture.StartAsync();
            var mic = test.Studio.Inputs["Mic"];
            var camera = test.Studio.Scenes["Gaming"].Items["Camera"];
            var read = new System.Collections.Concurrent.ConcurrentBag<Guid>();
            var held = 0;
            test.Responder.OnRequest = (type, data) =>
            {
                switch (type)
                {
                    case "SetSceneItemEnabled":
                        // OBS Studio makes the change and sends no event: the model learns of it by a read. The first change is held, and the line waits for its answer.
                        test.SetEnabled(Uuid(data, "sceneUuid"), Number(data, "sceneItemId"), data!.Value.GetProperty("sceneItemEnabled").GetBoolean());
                        return Interlocked.Exchange(ref held, 1) == 0 ? (-1, null) : (100, null);
                    case "GetSceneItemList":
                        read.Add(Uuid(data, "sceneUuid"));
                        return null;
                    default:
                        return null;
                }
            };
            var first = camera.SetEnabledAsync(false);
            await StudioRig.EventuallyAsync(() => test.Responder.HeldCount == 1, "the first change to be held");

            // The second change is asked for while the model shows the microphone in two scenes, and OBS Studio shows it in a third before the second change has its turn.
            var second = mic.SetEnabledInAllScenesAsync(false);
            var item = test.MakeItem(test.Spare, test.Mic, "Mic");
            await test.Responder.SendEventAsync("SceneItemCreated", new { sceneName = "Spare", sceneUuid = test.Spare, sourceName = "Mic", sourceUuid = test.Mic, sceneItemId = item, sceneItemIndex = 0 }, 128);
            await StudioRig.EventuallyAsync(() => test.Studio.Scenes["Spare"].Items.Count == 1, "the scene item of the third scene to be known");
            read.Clear();
            await test.Responder.AnswerHeldAsync();
            await first.WithTimeoutAsync(Timeout);
            await second.WithTimeoutAsync(Timeout);

            // The scene items that were changed are the ones that the model showed at its turn, and so are the scenes that are read when the events do not show the changes.
            Assert.False(test.Studio.Scenes["Spare"].Items["Mic"].IsEnabled);
            Assert.Contains(test.Spare, read);
        }

        [Fact]
        public async Task AnInput_IsShownOrHidden_InEverySceneThatShowsIt()
        {
            await using var test = await ByNameFixture.StartAsync();
            var mic = test.Studio.Inputs["Mic"];
            var asked = new List<string>();
            test.Responder.OnRequest = (type, data) =>
            {
                if (type != "SetSceneItemEnabled")
                {
                    return null;
                }

                asked.Add(data!.Value.GetRawText());
                test.SetEnabled(Uuid(data, "sceneUuid"), Number(data, "sceneItemId"), data.Value.GetProperty("sceneItemEnabled").GetBoolean());
                return (100, null);
            };

            await mic.SetEnabledInAllScenesAsync(false).WithTimeoutAsync(Timeout);

            Assert.All(mic.GetSceneItems(), item => Assert.False(item.IsEnabled));
            Assert.Equal(2, mic.GetSceneItems().Count);
            Assert.Equal(2, asked.Count);
            Assert.All(asked, entry => Assert.Contains("\"sceneItemEnabled\":false", entry, StringComparison.Ordinal));

            // One batch with both requests (a batch stops at its first refusal; separate requests would not).
            Assert.Single(test.Responder.BatchLog, batch => batch.SequenceEqual(["SetSceneItemEnabled", "SetSceneItemEnabled"]));

            await mic.SetEnabledInAllScenesAsync(true).WithTimeoutAsync(Timeout);

            Assert.All(mic.GetSceneItems(), item => Assert.True(item.IsEnabled));
            Assert.Equal(4, test.Count("SetSceneItemEnabled"));
        }

        [Fact]
        public async Task AnInput_ThatNoSceneItemShows_HasNothingToShowOrHide_AndTheCallReturnsWithoutARequest()
        {
            await using var test = await ByNameFixture.StartAsync();
            var desktop = test.Studio.Inputs["Desktop Audio"];
            var batches = test.Responder.Batches;

            await desktop.SetEnabledInAllScenesAsync(false).WithTimeoutAsync(Timeout);

            // Not even an empty batch is sent.
            Assert.Equal(0, test.Count("SetSceneItemEnabled"));
            Assert.Equal(batches, test.Responder.Batches);
        }

        [Fact]
        public async Task AnInput_ShownInASceneThatWasRemovedAndIsAlive_IsLeftAloneThere()
        {
            await using var test = await ByNameFixture.StartAsync();
            var mic = test.Studio.Inputs["Mic"];
            var chatting = test.Studio.Scenes["Chatting"];
            test.RemoveScene(test.Chatting);
            await StudioRig.EventuallyAsync(() => chatting.State == ObsObjectState.Detached, "the scene to be detached");
            test.Responder.OnRequest = (type, data) =>
            {
                if (type != "SetSceneItemEnabled")
                {
                    return null;
                }

                test.SetEnabled(Uuid(data, "sceneUuid"), Number(data, "sceneItemId"), data!.Value.GetProperty("sceneItemEnabled").GetBoolean());
                return (100, null);
            };

            await mic.SetEnabledInAllScenesAsync(false).WithTimeoutAsync(Timeout);

            // A scene that was removed is left alone by design (OBS Studio 33.0.0 accepts a change there, and a scene that shows it still renders it): only the scene item of the scene that is not removed was changed.
            Assert.Equal(1, test.Count("SetSceneItemEnabled"));
            Assert.False(test.Studio.Scenes["Gaming"].Items["Mic"].IsEnabled);
            Assert.True(chatting.Items["Mic"].IsEnabled);
        }

        [Fact]
        public async Task AnInput_WhoseSceneItemOBSStudioDoesNotKnow_IsARemovedException_ThatNamesTheSceneItem()
        {
            await using var test = await ByNameFixture.StartAsync();
            var mic = test.Studio.Inputs["Mic"];
            test.Responder.OnRequest = (type, data) => type == "SetSceneItemEnabled" ? (600, null) : null;

            var exception = await Assert.ThrowsAsync<ObsRemovedException>(() => mic.SetEnabledInAllScenesAsync(false).WithTimeoutAsync(Timeout));

            // The first request of the batch is the one that is refused (it halts at the first refusal), and it is the scene item of the microphone in "Gaming" (2) or in "Chatting" (1).
            Assert.Matches("^scene item (2 of scene 'Gaming'|1 of scene 'Chatting')$", exception.Description);
            Assert.Equal(ObsObjectState.Gone, exception.State);
        }

        [Fact]
        public async Task ARefusalBecauseOBSStudioIsNotReady_AfterTheFirstRequestOfAChange_DoesNotSayThatNothingWasChanged()
        {
            await using var test = await ByNameFixture.StartAsync();
            var mic = test.Studio.Inputs["Mic"];
            var requests = 0;
            test.Responder.OnRequest = (type, data) =>
            {
                if (type != "SetSceneItemEnabled")
                {
                    return null;
                }

                // The first scene item is changed; OBS Studio begins to change its scene collection, and is not ready for the second.
                if (Interlocked.Increment(ref requests) == 1)
                {
                    test.SetEnabled(Uuid(data, "sceneUuid"), Number(data, "sceneItemId"), false);
                    return (100, null);
                }

                return (207, null);
            };

            var exception = await Assert.ThrowsAsync<ObsResponseException>(() => mic.SetEnabledInAllScenesAsync(false).WithTimeoutAsync(Timeout));

            Assert.Equal(RequestStatusCode.NotReady, exception.ErrorCode);
            Assert.Contains("OBS Studio was not ready for request 2 of hiding input 'Mic' in every scene: it is changing its scene collection. The requests before it were made, and the ones after it were not.", exception.Message);
            Assert.DoesNotContain("Nothing was changed", exception.Message);
        }

        [Fact]
        public async Task ARefusalBecauseOBSStudioIsNotReady_OfTheFirstRequestOfAChange_SaysThatNothingWasChanged()
        {
            await using var test = await ByNameFixture.StartAsync();
            var mic = test.Studio.Inputs["Mic"];
            test.Responder.OnRequest = (type, data) => type == "SetSceneItemEnabled" ? (207, null) : null;

            var exception = await Assert.ThrowsAsync<ObsNotReadyException>(() => mic.SetEnabledInAllScenesAsync(false).WithTimeoutAsync(Timeout));

            Assert.Contains("hiding input 'Mic' in every scene: it is changing its scene collection. Nothing was changed.", exception.Message);
        }

        [Fact]
        public async Task TheProgramScene_WhenItIsRemoved_IsReplacedInTheModelWhenTheCallReturns()
        {
            await using var test = await ByNameFixture.StartAsync();
            var program = test.Studio.ProgramScene!;
            var hold = 1;
            test.Responder.OnRequest = ObsRemoves(test, (type, data) =>
            {
                switch (type)
                {
                    case "RemoveScene":
                        // OBS Studio makes the first scene of its list the program scene, and the model is told by a read: the answers to the reads are held.
                        test.RemoveScene(Uuid(data, "sceneUuid"));
                        test.Responder.Change(world => world.Program = world.Scenes[0].Uuid);
                        return (100, null);
                    case "GetCurrentProgramScene":
                        return Volatile.Read(ref hold) == 1 ? (-1, null) : null;
                    default:
                        return null;
                }
            });
            Assert.Equal("Scene", program.Name);

            var remove = program.RemoveAsync();
            await StudioRig.EventuallyAsync(() => test.Responder.HeldCount >= 1, "the read of the program scene to be held");
            await Task.Delay(200);

            // The scene is removed, and the model does not show the new program scene yet: the call has not returned.
            Assert.True(program.IsRemoved);
            Assert.False(remove.IsCompleted);
            Volatile.Write(ref hold, 0);
            await test.Responder.AnswerHeldAsync(type => type == "GetCurrentProgramScene" ? (100, new { sceneName = "Spare", sceneUuid = test.Spare }) : (100, null));
            await remove.WithTimeoutAsync(Timeout);

            Assert.NotSame(program, test.Studio.ProgramScene);
            Assert.False(program.IsProgram);
            Assert.True(test.Studio.ProgramScene!.IsProgram);
        }

        [Fact]
        public async Task AScene_RemovedRightAfterAQueuedSwitchToIt_ReturnsWhenTheModelHasReadTheProgramSceneThatOBSStudioHas()
        {
            // The switch is answered, and the model has not seen it (no event, and its reads are held back) when the removal has its turn: the model does not show the scene as the program
            // scene then, so a removal that waited for the program scene only when the model showed the scene as one would return with the program scene from before the switch.
            await using var test = await ByNameFixture.StartAsync();
            var spare = test.Studio.Scenes["Spare"];
            var before = test.Studio.ProgramScene!;
            var hold = 1;
            test.Responder.OnRequest = ObsRemoves(test, (type, data) =>
            {
                switch (type)
                {
                    case "SetCurrentProgramScene":
                        test.Responder.Change(world => world.Program = Uuid(data, "sceneUuid"));
                        return (100, null);
                    case "RemoveScene":
                        test.RemoveScene(Uuid(data, "sceneUuid"));
                        test.Responder.Change(world => world.Program = world.Scenes[0].Uuid);
                        return (100, null);
                    case "GetCurrentProgramScene":
                        return Volatile.Read(ref hold) == 1 ? (-1, null) : null;
                    default:
                        return null;
                }
            });
            Assert.Equal("Scene", before.Name);

            var switched = spare.SwitchToAsync();
            var remove = spare.RemoveAsync();
            await StudioRig.EventuallyAsync(() => spare.IsRemoved, "the scene to be removed in the model");
            await Task.Delay(300);

            // The model shows the scene removed, and the program scene that it had before the switch: OBS Studio has another one. The call has not returned: the model reads the program scene
            // after the removal, and that read is held back.
            Assert.False(remove.IsCompleted);
            Assert.True(before.IsProgram);
            Assert.True(test.Responder.HeldCount >= 1);
            Volatile.Write(ref hold, 0);
            await test.Responder.AnswerHeldAsync(type => type == "GetCurrentProgramScene" ? (100, new { sceneName = "Chatting", sceneUuid = test.Chatting }) : (100, null));
            await remove.WithTimeoutAsync(Timeout);
            await switched.WithTimeoutAsync(Timeout);

            Assert.False(before.IsProgram);
            Assert.Same(test.Studio.Scenes["Chatting"], test.Studio.ProgramScene);
        }

        [Fact]
        public async Task ASwitchAskedForAfterTheRemovalOfAScene_IsNotLookedAtBeforeTheProgramSceneWasReadAgain()
        {
            // Switch to B, remove B, switch to A, with the program reads held back: the model shows A (from before) all along, and OBS Studio ends with A too. The switch to A is about the
            // same part as the removal, so it waits behind it, and the removal waits for the read of the program scene: a switch that was looked at at once would return against what the
            // model showed before anything was read.
            await using var test = await ByNameFixture.StartAsync();
            var spare = test.Studio.Scenes["Spare"];
            var scene = test.Studio.Scenes["Scene"];
            Assert.True(scene.IsProgram);
            var hold = 1;
            test.Responder.OnRequest = ObsRemoves(test, (type, data) =>
            {
                switch (type)
                {
                    case "SetCurrentProgramScene":
                        test.Responder.Change(world => world.Program = Uuid(data, "sceneUuid"));
                        return (100, null);
                    case "RemoveScene":
                        test.RemoveScene(Uuid(data, "sceneUuid"));
                        test.Responder.Change(world => world.Program = world.Scenes[0].Uuid);
                        return (100, null);
                    case "GetCurrentProgramScene":
                        return Volatile.Read(ref hold) == 1 ? (-1, null) : null;
                    default:
                        return null;
                }
            });

            var first = spare.SwitchToAsync();
            var remove = spare.RemoveAsync();
            var last = scene.SwitchToAsync();
            await StudioRig.EventuallyAsync(() => spare.IsRemoved, "the scene to be removed in the model");
            await Task.Delay(300);

            Assert.False(remove.IsCompleted);
            Assert.False(last.IsCompleted);
            Volatile.Write(ref hold, 0);
            await test.Responder.AnswerHeldAsync(type => type == "GetCurrentProgramScene" ? (100, new { sceneName = "Scene", sceneUuid = scene.Uuid }) : (100, null));
            await Task.WhenAll(first, remove, last).WithTimeoutAsync(Timeout);

            Assert.True(scene.IsProgram);
        }

        [Fact]
        public async Task AProgramScene_WhoseSceneItemOBSStudioRefuses_EndsTheCallWhenTheModelHasReadTheProgramSceneThatOBSStudioHas()
        {
            await using var test = await ByNameFixture.StartAsync();
            var chatting = test.Studio.Scenes["Chatting"];
            test.Responder.Change(world => world.Program = test.Chatting);
            await test.Responder.SendEventAsync("CurrentProgramSceneChanged", new { sceneName = "Chatting", sceneUuid = test.Chatting }, 4);
            await StudioRig.EventuallyAsync(() => chatting.IsProgram, "the model to show the scene as the program scene");
            var hold = 1;
            test.Responder.OnRequest = ObsRemoves(test, (type, data) =>
            {
                switch (type)
                {
                    case "RemoveScene":
                        test.RemoveScene(Uuid(data, "sceneUuid"));
                        test.Responder.Change(world => world.Program = world.Scenes[0].Uuid);
                        return (100, null);
                    case "RemoveSceneItem":
                        return (600, null);
                    case "GetCurrentProgramScene":
                        return Volatile.Read(ref hold) == 1 ? (-1, null) : null;
                    default:
                        return null;
                }
            });

            var remove = chatting.RemoveAsync();
            await StudioRig.EventuallyAsync(() => chatting.IsRemoved, "the scene to be removed in the model");
            await StudioRig.EventuallyAsync(() => test.Responder.HeldCount >= 1, "the read of the program scene to be held");
            await Task.Delay(300);

            // The scene is removed, the scene item that shows it is refused, and the model still shows the scene as the program scene: the call has not ended with the refusal.
            Assert.True(chatting.IsProgram);
            Assert.False(remove.IsCompleted);
            Volatile.Write(ref hold, 0);
            await test.Responder.AnswerHeldAsync(type => type == "GetCurrentProgramScene" ? (100, new { sceneName = "Spare", sceneUuid = test.Spare }) : (100, null));
            var refused = await Assert.ThrowsAsync<ObsResponseException>(() => remove.WithTimeoutAsync(Timeout));

            Assert.Equal(RequestStatusCode.ResourceNotFound, refused.ErrorCode);
            Assert.False(chatting.IsProgram);
            Assert.Same(test.Studio.Scenes["Spare"], test.Studio.ProgramScene);
        }

        [Fact]
        public async Task ARefusalBecauseOBSStudioIsNotReady_OfTheFirstRequestOfTheRemovalOfADetachedObject_SaysThatNothingWasChanged()
        {
            await using var test = await ByNameFixture.StartAsync();
            var chatting = test.Studio.Scenes["Chatting"];
            test.RemoveScene(test.Chatting);
            await StudioRig.EventuallyAsync(() => chatting.State == ObsObjectState.Detached, "the scene to be detached");
            await test.DetachAsync("Overlay", test.Overlay);
            var overlay = test.FindDetached(test.Overlay);
            test.Responder.OnRequest = (type, data) => type == "RemoveSceneItem" ? (207, null) : null;

            // The first request of the batch is a scene item of a source that is removed already: nothing was changed, and the exception is the one that says so.
            var scene = await Assert.ThrowsAsync<ObsNotReadyException>(() => chatting.RemoveAsync().WithTimeoutAsync(Timeout));
            var input = await Assert.ThrowsAsync<ObsNotReadyException>(() => overlay.RemoveAsync().WithTimeoutAsync(Timeout));

            Assert.Contains("removing scene 'Chatting': it is changing its scene collection. Nothing was changed.", scene.Message);
            Assert.Contains("removing input 'Overlay': it is changing its scene collection. Nothing was changed.", input.Message);
            Assert.Equal(ObsObjectState.Detached, chatting.State);
        }

        [Fact]
        public async Task ARefusalOfTheFirstSceneItemOfADetachedObject_SaysThatNoRequestWasMadeBeforeIt()
        {
            await using var test = await ByNameFixture.StartAsync();
            await test.DetachAsync("Overlay", test.Overlay);
            var overlay = test.FindDetached(test.Overlay);
            test.Responder.OnRequest = (type, data) => type == "RemoveSceneItem" ? (600, null) : null;

            var exception = await Assert.ThrowsAsync<ObsResponseException>(() => overlay.RemoveAsync().WithTimeoutAsync(Timeout));

            Assert.Equal(RequestStatusCode.ResourceNotFound, exception.ErrorCode);
            Assert.Contains("None of the requests of this call were made before it: the source was removed already", exception.Message);
            Assert.DoesNotContain("The requests before it were made", exception.Message);
        }

        [Fact]
        public async Task AnInput_ThatWasRemoved_IsNotShownOrHidden()
        {
            await using var test = await ByNameFixture.StartAsync();
            await test.DetachAsync("Overlay", test.Overlay);

            var exception = await Assert.ThrowsAsync<ObsRemovedException>(() => test.FindDetached(test.Overlay).SetEnabledInAllScenesAsync(false).WithTimeoutAsync(Timeout));

            Assert.Equal(ObsObjectState.Detached, exception.State);
            Assert.Equal(0, test.Count("SetSceneItemEnabled"));
        }
    }
}
