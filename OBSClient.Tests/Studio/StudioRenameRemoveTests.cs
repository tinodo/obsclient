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
        public async Task AScene_WhoseNewNameIsTakenByASceneThatWasRemovedAndIsAlive_WithObsWebsocket56_IsANameInUseException_ThatSaysWhatKeepsItAlive()
        {
            // Before obs-websocket 5.7 the name of a scene that was removed stays taken for as long as the scene is alive.
            await using var test = await ByNameFixture.StartAsync(webSocketVersion: "5.6.3");
            var chatting = test.Studio.Scenes["Chatting"];
            test.RemoveScene(test.Chatting);
            await StudioRig.EventuallyAsync(() => chatting.State == ObsObjectState.Detached, "the scene to be detached");
            test.Responder.OnRequest = (type, data) => type == "SetSceneName" ? (601, null) : null;

            var exception = await Assert.ThrowsAsync<ObsNameInUseException>(() => test.Studio.Scenes["Spare"].RenameAsync("Chatting").WithTimeoutAsync(Timeout));

            Assert.Same(chatting, exception.HeldBy);
            Assert.Contains("a scene named 'Chatting' was removed, but OBS Studio keeps it alive because 1 scene item still shows it (scene item 4 of scene 'Gaming')", exception.Message);
            Assert.Contains("Call RemoveAsync() on the scene (HeldBy of this exception)", exception.Message);
        }

        [Fact]
        public async Task AScene_WhoseNewNameIsThatOfASceneThatWasRemovedAndIsAlive_WithObsWebsocket57_IsNotBlamedOnThatScene()
        {
            // OBS Studio 32 takes the name of a scene that was removed out of the list of its canvas at once (obs_canvas_remove_source), and obs-websocket 5.7 looks the name up there: whatever
            // refused the name, it was not that scene.
            await using var test = await ByNameFixture.StartAsync();
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
            Assert.Contains("The group 'Group' can not be renamed through the scene", refused.Message);
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

            // A scene that was removed does not take requests: only the scene item of the scene that is not removed was changed.
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
