namespace OBSStudioClient.Tests.Live
{
    using System.Globalization;
    using System.Text.Json;
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Studio;
    using OBSStudioClient.Tests.TestSupport;
    using Xunit.Abstractions;

    /// <summary>
    /// What the studio layer does against a real OBS Studio: it makes, changes, switches to and removes scenes, inputs and scene items by name, it follows what the user does in OBS Studio
    /// (the plain client plays the user), and after each step the model has to equal what OBS Studio holds. They also check against the real thing what the model assumes of it where the
    /// simulation only had what was read in the source of OBS Studio: when a name is taken, what a removed scene or input does to its name, and what OBS Studio does with the program scene.
    /// Everything that is made carries the name prefix of <see cref="LiveObs.NamePrefix"/> and is removed again; the scene that was on the program output is put back.
    /// </summary>
    [Collection("Live OBS")]
    public class LiveStudioTests(ITestOutputHelper output)
    {
        private static readonly TimeSpan Wait = TimeSpan.FromSeconds(20);

        private static object ColorSettings => new { color = 4278190335L, width = 640, height = 360 };

        // A call that removes a scene or an input returns when the model shows it removed, and the model can call it Gone a moment later: when the events were late, it reads OBS Studio once more before it
        // calls an object destroyed.
        private static async Task AssertGoneAsync(ObsSource source)
        {
            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => source.State == ObsObjectState.Gone, Wait), $"The {source.Label} was not destroyed (it is {source.State}).");
        }

        [LiveUuidChangesFact]
        public async Task ScenesInputsAndSceneItems_AreMadeChangedSwitchedAndRemovedByName_AndTheModelEqualsWhatObsStudioHoldsAfterEveryStep()
        {
            await using var live = await LiveStudio.ConnectAsync(output);
            var studio = live.Studio;
            output.WriteLine($"OBS Studio {live.ObsVersionText}, obs-websocket {live.WebSocketVersionText}.");
            await live.AssertModelEqualsObsAsync("at connect");
            var original = studio.ProgramScene;
            Assert.NotNull(original);
            var kind = await live.ColorKindAsync();

            // Two scenes, and an input in the first one.
            var sceneName = LiveObs.UniqueName("scene");
            var otherName = LiveObs.UniqueName("other");
            var inputName = LiveObs.UniqueName("color");
            var scene = await studio.CreateSceneAsync(sceneName);
            var other = await studio.CreateSceneAsync(otherName);
            Assert.Same(scene, studio.Scenes[sceneName]);
            Assert.Same(other, studio.Scenes[otherName]);
            var input = await scene.CreateInputAsync(inputName, kind, ColorSettings);
            Assert.Same(input, studio.Inputs[inputName]);
            Assert.Equal(kind, input.Kind);
            Assert.Same(input, scene.Items[inputName].Source);
            await live.AssertModelEqualsObsAsync("after two scenes and an input were made");

            // The first scene and the input are shown in the other scene: a scene that shows a scene, and one input in two scenes.
            var nested = await other.AddAsync(scene);
            var shown = await other.AddAsync(input, enabled: false);
            Assert.Same(scene, nested.Source);
            Assert.Same(input, shown.Source);
            Assert.False(shown.IsEnabled);
            Assert.Equal(2, input.GetSceneItems().Count);
            await live.AssertModelEqualsObsAsync("after scene items were added");

            // A scene item is found by the name of its source, and changed.
            Assert.True(await other.Items[inputName].ToggleEnabledAsync());
            Assert.True(other.Items[inputName].IsEnabled);
            await other.Items[inputName].SetLockedAsync(true);
            Assert.True(other.Items[inputName].IsLocked);
            await other.Items[inputName].SetIndexAsync(0);
            Assert.Equal(0, other.Items[inputName].Index);
            await live.AssertModelEqualsObsAsync("after a scene item was shown, locked and moved");

            // The input in every scene that shows it, in one call.
            await input.SetEnabledInAllScenesAsync(false);
            Assert.All(input.GetSceneItems(), item => Assert.False(item.IsEnabled));
            await live.AssertModelEqualsObsAsync("after the input was hidden in every scene");
            await input.SetEnabledInAllScenesAsync(true);
            Assert.All(input.GetSceneItems(), item => Assert.True(item.IsEnabled));
            await live.AssertModelEqualsObsAsync("after the input was shown in every scene");

            // Renames, by name.
            var renamedInput = inputName + "-renamed";
            var renamedScene = sceneName + "-renamed";
            await input.RenameAsync(renamedInput);
            await scene.RenameAsync(renamedScene);
            Assert.Same(input, studio.Inputs[renamedInput]);
            Assert.False(studio.Inputs.Contains(inputName));
            Assert.Same(scene, studio.Scenes[renamedScene]);
            Assert.False(studio.Scenes.Contains(sceneName));
            await live.AssertModelEqualsObsAsync("after the renames");

            // The program scene.
            await other.SwitchToAsync();
            Assert.True(other.IsProgram);
            await live.AssertModelEqualsObsAsync("after a switch to a scene");
            await original!.SwitchToAsync();
            Assert.True(original.IsProgram);
            await live.AssertModelEqualsObsAsync("after the switch back");

            // Removal: the input goes with the scene items that show it, and OBS Studio destroys it; then the scenes.
            await input.RemoveAsync();
            await AssertGoneAsync(input);
            Assert.False(studio.Inputs.Contains(renamedInput));
            Assert.Empty(scene.Items);
            await live.AssertModelEqualsObsAsync("after the input was removed");
            await other.RemoveAsync();
            await scene.RemoveAsync();
            await AssertGoneAsync(other);
            await AssertGoneAsync(scene);
            await live.AssertModelEqualsObsAsync("after the scenes were removed");

            // The names are free again.
            var again = await studio.CreateSceneAsync(renamedScene);
            var againInput = await again.CreateInputAsync(renamedInput, kind, ColorSettings);
            Assert.NotSame(scene, again);
            Assert.NotSame(input, againInput);
            await live.AssertModelEqualsObsAsync("after the names were used again");
        }

        [LiveUuidChangesFact]
        public async Task AnInputWithAudio_IsMutedAndGivenAVolume_AsOBSStudioHoldsIt()
        {
            await using var live = await LiveStudio.ConnectAsync(output);
            var studio = live.Studio;
            Assert.Contains("ffmpeg_source", await live.Client.GetInputKindList());
            var scene = await studio.CreateSceneAsync(LiveObs.UniqueName("audio"));

            // The media source has audio in every OBS Studio, and no file is needed to make one. The audio state is complete when the call returns.
            var input = await scene.CreateInputAsync(LiveObs.UniqueName("media"), "ffmpeg_source", new { is_local_file = true });
            Assert.True(input.HasAudio);
            Assert.False(input.IsMuted);
            Assert.Equal(0.0, input.VolumeDb, 0.01);
            Assert.Equal(1.0, input.VolumeMul, 0.001);

            var reference = InputRef.FromUuid(input.Uuid);
            await input.SetMutedAsync(true);
            Assert.True(input.IsMuted);
            Assert.True(await live.Client.GetInputMute(reference));
            Assert.False(await input.ToggleMuteAsync());
            Assert.False(input.IsMuted);
            Assert.False(await live.Client.GetInputMute(reference));

            await input.SetVolumeDbAsync(-6.0);
            var volume = await live.Client.GetInputVolume(reference);
            Assert.Equal(volume.InputVolumeDb, input.VolumeDb, 0.01);
            Assert.Equal(volume.InputVolumeMul, input.VolumeMul, 0.001);
            Assert.Equal(-6.0, input.VolumeDb, 0.05);

            await input.SetVolumeMulAsync(0.5);
            volume = await live.Client.GetInputVolume(reference);
            Assert.Equal(0.5, input.VolumeMul, 0.001);
            Assert.Equal(volume.InputVolumeDb, input.VolumeDb, 0.01);
            await live.AssertModelEqualsObsAsync("after the audio was changed");
        }

        [LiveUuidChangesFact]
        public async Task ANameThatIsTaken_IsRefusedAsANameInUseException_ThatNamesWhatHoldsIt()
        {
            await using var live = await LiveStudio.ConnectAsync(output);
            var studio = live.Studio;
            output.WriteLine($"OBS Studio {live.ObsVersionText}, obs-websocket {live.WebSocketVersionText}.");
            var kind = await live.ColorKindAsync();
            var sceneName = LiveObs.UniqueName("scene");
            var spareName = LiveObs.UniqueName("spare");
            var inputName = LiveObs.UniqueName("color");
            var scene = await studio.CreateSceneAsync(sceneName);
            var spare = await studio.CreateSceneAsync(spareName);
            var input = await scene.CreateInputAsync(inputName, kind, ColorSettings);

            // A scene by the name of a scene, an input by the name of an input, and an input by the name of a scene.
            var sceneTaken = await Assert.ThrowsAsync<ObsNameInUseException>(() => studio.CreateSceneAsync(sceneName));
            Assert.Same(scene, sceneTaken.HeldBy);
            var inputTaken = await Assert.ThrowsAsync<ObsNameInUseException>(() => scene.CreateInputAsync(inputName, kind));
            Assert.Same(input, inputTaken.HeldBy);
            var inputByScene = await Assert.ThrowsAsync<ObsNameInUseException>(() => scene.CreateInputAsync(sceneName, kind));
            Assert.Same(scene, inputByScene.HeldBy);
            Assert.Contains("an input can not have the name of a scene", inputByScene.Message);

            // Renames to a name that is taken, and to the name that the object has already (OBS Studio refuses that as well).
            var renameTaken = await Assert.ThrowsAsync<ObsNameInUseException>(() => spare.RenameAsync(sceneName));
            Assert.Same(scene, renameTaken.HeldBy);
            var renameOwn = await Assert.ThrowsAsync<ObsNameInUseException>(() => spare.RenameAsync(spareName));
            Assert.Same(spare, renameOwn.HeldBy);
            var inputRenameTaken = await Assert.ThrowsAsync<ObsNameInUseException>(() => input.RenameAsync(sceneName));
            Assert.Same(scene, inputRenameTaken.HeldBy);
            await live.AssertModelEqualsObsAsync("after the refusals (nothing was changed)");

            // A scene by the name of an input: obs-websocket 5.7 and newer allow it (scenes are looked up in their canvas), older versions refuse it.
            if (live.WebSocketVersion >= new Version(5, 7, 0))
            {
                var sameName = await studio.CreateSceneAsync(inputName);
                Assert.Same(sameName, studio.Scenes[inputName]);
                Assert.Same(input, studio.Inputs[inputName]);
                await live.AssertModelEqualsObsAsync("after a scene was made with the name of an input");
            }
            else
            {
                var refused = await Assert.ThrowsAsync<ObsNameInUseException>(() => studio.CreateSceneAsync(inputName));
                Assert.Same(input, refused.HeldBy);
            }
        }

        [LiveUuidChangesFact]
        public async Task ASceneThatWasRemovedAndIsAlive_GivesUpItsName_FromObsStudio31_1_AndKeepsItBefore()
        {
            await using var live = await LiveStudio.ConnectAsync(output);
            var studio = live.Studio;
            output.WriteLine($"OBS Studio {live.ObsVersionText}, obs-websocket {live.WebSocketVersionText}.");
            var name = LiveObs.UniqueName("removed");
            var holder = await studio.CreateSceneAsync(LiveObs.UniqueName("holder"));
            var scene = await studio.CreateSceneAsync(name);

            // A scene item of another scene shows the scene, so OBS Studio keeps it alive when the user removes it.
            await holder.AddAsync(scene);
            await live.Client.RemoveScene(SceneRef.FromUuid(scene.Uuid));
            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => scene.State == ObsObjectState.Detached, Wait), "The scene was not detached.");
            Assert.False(studio.Scenes.Contains(name));

            if (live.ObsVersion >= new Version(31, 1, 0))
            {
                // The name is free at once.
                var again = await studio.CreateSceneAsync(name);
                Assert.NotSame(scene, again);
                Assert.Same(again, studio.Scenes[name]);
                Assert.Equal(ObsObjectState.Detached, scene.State);
            }
            else
            {
                var taken = await Assert.ThrowsAsync<ObsNameInUseException>(() => studio.CreateSceneAsync(name));
                Assert.Same(scene, taken.HeldBy);
                await scene.RemoveAsync();
                await AssertGoneAsync(scene);
                var again = await studio.CreateSceneAsync(name);
                Assert.Same(again, studio.Scenes[name]);
            }

            await live.AssertModelEqualsObsAsync("after the name was used again");
        }

        [LiveUuidChangesFact]
        public async Task ASceneThatWasRemovedAndIsAlive_IsRemovedForGoodByItsSceneItems_WhateverOBSStudioAnswersToAnotherRemoveScene()
        {
            // What ObsScene.RemoveAsync relies on for a scene that the model took for removed without a word of OBS Studio (a list of scenes did not have it): it asks OBS Studio to remove the scene first, and
            // then removes the scene items that show it. OBS Studio 33.0.0 refuses to remove a scene that it removed already and keeps alive (701: it has no canvas for it any more); the call has to go on
            // either way (an OBS Studio that answered with success would do no harm), and the scene is destroyed with its last scene item.
            await using var live = await LiveStudio.ConnectAsync(output);
            var studio = live.Studio;
            output.WriteLine($"OBS Studio {live.ObsVersionText}, obs-websocket {live.WebSocketVersionText}.");
            var holder = await studio.CreateSceneAsync(LiveObs.UniqueName("holder"));
            var scene = await studio.CreateSceneAsync(LiveObs.UniqueName("removed"));
            await holder.AddAsync(scene);
            await live.Client.RemoveScene(SceneRef.FromUuid(scene.Uuid));
            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => scene.State == ObsObjectState.Detached, Wait), "The scene was not detached.");

            ObsResponseException? refused = null;
            try
            {
                await live.Client.RemoveScene(SceneRef.FromUuid(scene.Uuid));
            }
            catch (ObsResponseException exception)
            {
                refused = exception;
            }

            output.WriteLine(refused is null ? "Another RemoveScene of the scene that was removed and is alive: answered with success." : $"Another RemoveScene of the scene that was removed and is alive: refused, {(int)refused.ErrorCode} {refused.ErrorCode}: {refused.ErrorMessage}");
            Assert.True(refused is null || refused.ErrorCode == RequestStatusCode.ResourceActionFailed, "OBS Studio refused to remove the scene again with a code that the studio layer does not expect.");

            await scene.RemoveAsync();

            await AssertGoneAsync(scene);
            await live.AssertModelEqualsObsAsync("after the scene was removed for good");
        }

        [LiveUuidChangesFact]
        public async Task AnInputThatWasRemovedAndIsAlive_KeepsItsName_UntilItsLastSceneItemIsGone()
        {
            await using var live = await LiveStudio.ConnectAsync(output);
            var studio = live.Studio;
            var kind = await live.ColorKindAsync();
            var name = LiveObs.UniqueName("removed");
            var scene = await studio.CreateSceneAsync(LiveObs.UniqueName("scene"));
            var other = await studio.CreateSceneAsync(LiveObs.UniqueName("other"));
            var input = await scene.CreateInputAsync(name, kind, ColorSettings);
            await other.AddAsync(input);

            // The user removes the input; the scene items keep it alive, still listed, and its name stays taken.
            await live.Client.RemoveInput(InputRef.FromUuid(input.Uuid));
            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => input.State == ObsObjectState.Detached, Wait), "The input was not detached.");
            Assert.False(studio.Inputs.Contains(name));
            await live.AssertModelEqualsObsAsync("after the user removed the input");

            var taken = await Assert.ThrowsAsync<ObsNameInUseException>(() => scene.CreateInputAsync(name, kind));
            Assert.Same(input, taken.HeldBy);
            Assert.Contains("keeps it alive", taken.Message);
            await live.AssertModelEqualsObsAsync("after the name was refused");

            // Removing it removes the scene items that are left, OBS Studio destroys it, and the name is free.
            await input.RemoveAsync();
            await AssertGoneAsync(input);
            var again = await scene.CreateInputAsync(name, kind, ColorSettings);
            Assert.NotSame(input, again);
            await live.AssertModelEqualsObsAsync("after the name was used again");
        }

        [LiveUuidChangesFact]
        public async Task TheProgramScene_WhenItIsRemoved_IsReplacedInTheModel_AsOBSStudioReplacesIt()
        {
            await using var live = await LiveStudio.ConnectAsync(output);
            var studio = live.Studio;
            var scene = await studio.CreateSceneAsync(LiveObs.UniqueName("program"));
            await scene.SwitchToAsync();
            Assert.True(scene.IsProgram);
            await live.AssertModelEqualsObsAsync("after the switch");

            await scene.RemoveAsync();

            // The call returns when the model shows the scene that OBS Studio made the program scene instead.
            await AssertGoneAsync(scene);
            Assert.NotNull(studio.ProgramScene);
            Assert.NotSame(scene, studio.ProgramScene);
            Assert.Equal((await live.Client.GetCurrentProgramScene()).SceneUuid, studio.ProgramScene!.Uuid);
            output.WriteLine($"OBS Studio made '{studio.ProgramScene.Name}' the program scene.");
            await live.AssertModelEqualsObsAsync("after the program scene was removed");
        }

        [LiveUuidChangesFact]
        public async Task RemovingAnInputThatTheProgramSceneShows_IsDoneInOneCall_OrWhenItMeetsASceneItemThatIsGone_InTwo()
        {
            const int Rounds = 60;
            await using var live = await LiveStudio.ConnectAsync(output);
            var studio = live.Studio;
            var kind = await live.ColorKindAsync();
            var program = await studio.CreateSceneAsync(LiveObs.UniqueName("program"));
            var quiet = await studio.CreateSceneAsync(LiveObs.UniqueName("quiet"));
            await program.SwitchToAsync();

            // OBS Studio takes the scene items of a removed input out of the scenes that it renders on its next frame, so the scene item of the program scene can be gone between the
            // request that removes the input and the one that removes that scene item: OBS Studio then answers 600, the batch stops, and the second call removes what is left.
            var races = 0;
            for (var round = 1; round <= Rounds; round++)
            {
                var input = await program.CreateInputAsync(LiveObs.UniqueName($"input{round}"), kind, ColorSettings);
                await quiet.AddAsync(input);
                try
                {
                    await input.RemoveAsync();
                }
                catch (ObsResponseException exception) when (exception.ErrorCode == RequestStatusCode.ResourceNotFound)
                {
                    races++;
                    Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => input.State == ObsObjectState.Detached, Wait), "The input was not detached.");
                    await input.RemoveAsync();
                }

                await AssertGoneAsync(input);
            }

            output.WriteLine($"{races} of {Rounds} removals met a scene item that was gone already (the race with the frame of OBS Studio {live.ObsVersionText}).");
            await live.AssertModelEqualsObsAsync("after the removals");
            output.WriteLine($"OBS Studio still lists {live.StillListedRemovedInputs.Count} of the {Rounds} inputs that were removed (it keeps some alive: {string.Join(", ", live.StillListedRemovedInputs)}).");
        }

        [LiveUuidChangesFact]
        public async Task WhatTheUserDoesInObsStudio_IsFollowedByTheModel_AndTheModelEqualsWhatObsStudioHolds()
        {
            await using var live = await LiveStudio.ConnectAsync(output);
            var studio = live.Studio;
            var raw = live.Client;
            var kind = await live.ColorKindAsync();
            var original = studio.ProgramScene;
            Assert.NotNull(original);
            var sceneName = LiveObs.UniqueName("scene");
            var otherName = LiveObs.UniqueName("other");
            var inputName = LiveObs.UniqueName("color");

            // Scenes and an input, made in OBS Studio.
            var sceneUuid = await raw.CreateScene(sceneName);
            var scene = SceneRef.FromUuid(sceneUuid);
            await live.AssertModelEqualsObsAsync("after the user made a scene");
            Assert.True(studio.Scenes.Contains(sceneName));
            var created = await raw.CreateInput(scene, inputName, kind, new Dictionary<string, object> { ["color"] = 4278190335L, ["width"] = 640, ["height"] = 360 });
            await live.AssertModelEqualsObsAsync("after the user made an input");
            Assert.Same(studio.Inputs[inputName], studio.Scenes[sceneName].Items[inputName].Source);

            // Another scene shows the first one and the input.
            var otherUuid = await raw.CreateScene(otherName);
            var other = SceneRef.FromUuid(otherUuid);
            await raw.CreateSceneItem(other, SourceRef.FromUuid(sceneUuid));
            var shownId = await raw.CreateSceneItem(other, SourceRef.FromUuid(created.InputUuid), sceneItemEnabled: false);
            await live.AssertModelEqualsObsAsync("after the user made scene items");
            Assert.Equal(2, studio.Scenes[otherName].Items.Count);

            // Changes of a scene item, and renames.
            await raw.SetSceneItemEnabled(other, shownId, true);
            await raw.SetSceneItemLocked(other, shownId, true);
            await raw.SetSceneItemIndex(other, shownId, 0);
            await live.AssertModelEqualsObsAsync("after the user changed a scene item");
            await raw.SetInputName(InputRef.FromUuid(created.InputUuid), inputName + "-renamed");
            await raw.SetSceneName(scene, sceneName + "-renamed");
            await live.AssertModelEqualsObsAsync("after the user renamed an input and a scene");
            Assert.True(studio.Inputs.Contains(inputName + "-renamed"));
            Assert.True(studio.Scenes.Contains(sceneName + "-renamed"));

            // The program scene.
            await raw.SetCurrentProgramScene(other);
            await live.AssertModelEqualsObsAsync("after the user switched scenes");
            Assert.Equal(otherUuid, studio.ProgramScene!.Uuid);
            await raw.SetCurrentProgramScene(SceneRef.FromUuid(original!.Uuid));
            await live.AssertModelEqualsObsAsync("after the user switched back");

            // Removals: the input stays alive for the scene items that show it, and goes when the last of them goes; then the scenes. OBS Studio goes on listing the input, so the comparison alone does not
            // show that the model noticed the removal: the model has to call the input Detached, and not show it among the inputs any more.
            var modelInput = studio.Inputs[inputName + "-renamed"];
            await raw.RemoveInput(InputRef.FromUuid(created.InputUuid));
            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => modelInput.State == ObsObjectState.Detached && !studio.Inputs.TryGet(modelInput.Uuid, out _), Wait), "The model did not show the input as removed (Detached, and not among the inputs).");
            await live.AssertModelEqualsObsAsync("after the user removed the input");
            await raw.RemoveSceneItem(other, shownId);
            await live.AssertModelEqualsObsAsync("after the user removed a scene item of the input");
            await raw.RemoveSceneItem(scene, created.SceneItemId);
            await AssertGoneAsync(modelInput);
            await live.AssertModelEqualsObsAsync("after the user removed the last scene item of the input");
            Assert.DoesNotContain(studio.Scenes.SelectMany(candidate => candidate.Items), item => item.Source is ObsInput input && input.Uuid == created.InputUuid);
            await raw.RemoveScene(other);
            await live.AssertModelEqualsObsAsync("after the user removed a scene");
            await raw.RemoveScene(scene);
            await live.AssertModelEqualsObsAsync("after the user removed the other scene");
        }

        [LiveUuidChangesFact]
        public async Task TheFilters_AreMadeChangedMovedAndRemovedByName_AndTheModelEqualsWhatObsStudioHoldsAfterEveryStep()
        {
            await using var live = await LiveStudio.ConnectAsync(output);
            var studio = live.Studio;
            var kinds = await live.Client.GetSourceFilterKindList();
            const string Kind = "color_filter_v2";
            Assert.Contains(Kind, kinds);
            var sceneName = LiveObs.UniqueName("scene");
            var inputName = LiveObs.UniqueName("color");
            var scene = await studio.CreateSceneAsync(sceneName);
            var input = await scene.CreateInputAsync(inputName, await live.ColorKindAsync(), ColorSettings);
            await live.AssertModelEqualsObsAsync("after a scene and an input were made");
            Assert.Empty(input.Filters);

            // Made by name, on top of each other.
            var first = await input.CreateFilterAsync("first", Kind, new { gamma = 0.25 });
            var second = await input.CreateFilterAsync("second", Kind);
            Assert.Same(first, input.Filters["first"]);
            Assert.Equal(new[] { "first", "second" }, input.Filters.Select(filter => filter.Name).ToArray());
            Assert.Equal(new[] { 0, 1 }, input.Filters.Select(filter => filter.Index).ToArray());
            Assert.All(input.Filters, filter => Assert.True(filter.IsEnabled));
            Assert.All(input.Filters, filter => Assert.Equal(Kind, filter.Kind));
            await live.AssertModelEqualsObsAsync("after two filters were made");
            var settings = (await live.Client.GetSourceFilter(SourceRef.FromUuid(input.Uuid), "first")).FilterSettings;
            Assert.Equal(0.25, double.Parse(JsonSerializer.Serialize(settings["gamma"]), CultureInfo.InvariantCulture));

            // Enabled, disabled and toggled.
            await first.SetEnabledAsync(false);
            Assert.False(first.IsEnabled);
            await live.AssertModelEqualsObsAsync("after a filter was disabled");
            Assert.True(await first.ToggleEnabledAsync());
            Assert.True(first.IsEnabled);
            Assert.False(await second.ToggleEnabledAsync());
            await live.AssertModelEqualsObsAsync("after the filters were toggled");

            // Moved and renamed.
            await second.SetIndexAsync(0);
            Assert.Equal(0, second.Index);
            Assert.Equal(1, first.Index);
            Assert.Equal(new[] { "second", "first" }, input.Filters.Select(filter => filter.Name).ToArray());
            await live.AssertModelEqualsObsAsync("after a filter was moved");
            await first.RenameAsync("renamed");
            Assert.Equal("renamed", first.Name);
            Assert.Same(first, input.Filters["renamed"]);
            Assert.False(input.Filters.Contains("first"));
            await live.AssertModelEqualsObsAsync("after a filter was renamed");

            // A change that is asked for right after a rename names the filter by its new name.
            var rename = first.RenameAsync("again");
            var enable = first.SetEnabledAsync(false);
            await rename;
            await enable;
            Assert.Equal("again", first.Name);
            Assert.False(first.IsEnabled);
            await live.AssertModelEqualsObsAsync("after a rename and a change right behind it");

            // A name that is taken.
            var inUse = await Assert.ThrowsAsync<ObsNameInUseException>(() => second.RenameAsync("again"));
            Assert.Equal("again", inUse.Name);
            await Assert.ThrowsAsync<ObsNameInUseException>(() => input.CreateFilterAsync("again", Kind));

            // A scene has filters too.
            var onScene = await scene.CreateFilterAsync("scene filter", Kind);
            Assert.Equal(new[] { "scene filter" }, scene.Filters.Select(filter => filter.Name).ToArray());
            await live.AssertModelEqualsObsAsync("after a filter was made on a scene");

            // Removed.
            await second.RemoveAsync();
            Assert.True(second.IsRemoved);
            Assert.Equal(new[] { "again" }, input.Filters.Select(filter => filter.Name).ToArray());
            Assert.Equal(0, first.Index);
            await Assert.ThrowsAsync<ObsRemovedException>(() => second.SetEnabledAsync(true));
            await live.AssertModelEqualsObsAsync("after a filter was removed");
            await onScene.RemoveAsync();
            await live.AssertModelEqualsObsAsync("after the filter of the scene was removed");
        }

        [LiveUuidChangesFact]
        public async Task WhatTheUserDoesToFiltersInObsStudio_IsFollowedByTheModel_AndTheModelEqualsWhatObsStudioHolds()
        {
            await using var live = await LiveStudio.ConnectAsync(output);
            var studio = live.Studio;
            var raw = live.Client;
            const string Kind = "color_filter_v2";
            var inputName = LiveObs.UniqueName("color");
            var scene = await studio.CreateSceneAsync(LiveObs.UniqueName("scene"));
            var input = await scene.CreateInputAsync(inputName, await live.ColorKindAsync(), ColorSettings);
            var source = SourceRef.FromUuid(input.Uuid);

            // Made, in OBS Studio.
            await raw.CreateSourceFilter(source, "one", Kind);
            await raw.CreateSourceFilter(source, "two", Kind);
            await raw.CreateSourceFilter(source, "three", Kind);
            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => input.Filters.Count == 3, Wait), "The model did not show the filters that the user made.");
            await live.AssertModelEqualsObsAsync("after the user made three filters");
            var one = input.Filters["one"];
            var two = input.Filters["two"];

            // Enabled and moved.
            await raw.SetSourceFilterEnabled(source, "one", false);
            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => !one.IsEnabled, Wait), "The model did not show that the user disabled a filter.");
            await raw.SetSourceFilterIndex(source, "three", 0);
            await live.AssertModelEqualsObsAsync("after the user disabled a filter and moved another");
            Assert.Equal(new[] { "three", "one", "two" }, input.Filters.Select(filter => filter.Name).ToArray());

            // Renamed: the same object.
            await raw.SetSourceFilterName(source, "two", "deux");
            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => two.Name == "deux", Wait), "The model did not show that the user renamed a filter.");
            await live.AssertModelEqualsObsAsync("after the user renamed a filter");
            Assert.Same(two, input.Filters["deux"]);

            // Removed.
            await raw.RemoveSourceFilter(source, "deux");
            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => two.IsRemoved, Wait), "The model did not show that the user removed a filter.");
            await live.AssertModelEqualsObsAsync("after the user removed a filter");
            Assert.Equal(new[] { "three", "one" }, input.Filters.Select(filter => filter.Name).ToArray());

            // The filters of an input that is removed with its scene items go with it.
            await input.RemoveAsync();
            await AssertGoneAsync(input);
            Assert.Empty(input.Filters);
            Assert.True(one.IsRemoved);
            await live.AssertModelEqualsObsAsync("after the input was removed");
        }

        [LiveUuidChangesFact]
        public async Task TheRecording_IsStartedAndStopped_AsOBSStudioDoesIt_AndTheModelShowsEveryStep()
        {
            await using var live = await LiveStudio.ConnectAsync(output);
            var studio = live.Studio;
            var record = studio.Outputs.Record;
            Assert.All(studio.Outputs, candidate => Assert.False(candidate.IsActive, $"The OBS Studio of the live tests is running its {candidate.Name} already."));

            var steps = new List<ObsOutputStatus>();
            studio.Client.RecordStateChanged += (_, e) => output.WriteLine($"RecordStateChanged: {e.OutputState} (active {e.OutputActive}) {e.OutputPath}");
            studio.Changed += (_, e) =>
            {
                if (ReferenceEquals(e.Entity, record) && e.PropertyName == nameof(ObsOutput.Status))
                {
                    lock (steps)
                    {
                        steps.Add((ObsOutputStatus)e.NewValue!);
                    }
                }
            };

            // The recording goes to a folder that this test makes and removes: the profile of a fresh OBS Studio has the videos folder of the user.
            var folder = Path.Combine(Path.GetTempPath(), "obsclient-live-" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(folder);
            var settings = new[] { (Category: "SimpleOutput", Name: "FilePath", Value: folder) };
            var before = new List<(string Category, string Name, string? Value)>();
            var paused = false;
            try
            {
                foreach (var (category, name, value) in settings)
                {
                    before.Add((category, name, (await live.Client.GetProfileParameter(category, name)).ParameterValue));
                    await live.Client.SetProfileParameter(category, name, value);
                }

                await record.StartAsync();
                var started = await AsyncTestHelpers.WaitUntilAsync(() => record.Status == ObsOutputStatus.Started, Wait);
                var status = await live.Client.GetRecordStatus();
                output.WriteLine($"After the start: the model says {record.Status}; OBS Studio says active {status.OutputActive}, paused {status.OutputPaused}, {status.OutputTimecode}.");
                Assert.True(started, $"The recording did not start: it is {record.Status}.");
                Assert.True(status.OutputActive);

                // A recording that uses the encoder of the stream (the default of the simple output) cannot be paused: OBS Studio answers that it paused it, and sends no event, and the recording goes on.
                // The call returns with what OBS Studio holds, so one of the two follows, whatever the OBS Studio and its settings.
                await record.PauseAsync();
                paused = record.IsPaused;
                Assert.Equal(paused, (await live.Client.GetRecordStatus()).OutputPaused);
                if (paused)
                {
                    Assert.True(record.IsActive);
                    await record.ResumeAsync();
                    Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => record.Status == ObsOutputStatus.Started, Wait), $"The recording did not resume: it is {record.Status}.");
                }
                else
                {
                    var refused = await Assert.ThrowsAsync<ObsResponseException>(() => record.ResumeAsync());
                    Assert.Equal(RequestStatusCode.OutputNotPaused, refused.ErrorCode);
                    Assert.Equal(ObsOutputStatus.Started, record.Status);
                }

                output.WriteLine(paused ? "The recording was paused and resumed." : "The recording was not paused (it shares the encoder of the stream), and could not be resumed.");

                await record.StopAsync();
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => record.Status == ObsOutputStatus.Stopped, Wait), $"The recording did not stop: it is {record.Status}.");
                // OBS Studio says that the recording stopped a moment before it stops saying that it runs (seen with OBS Studio 33.0.0: the status was still active when the event came). The model
                // goes by the event, so it does not take that status for a change.
                var waited = 0;
                while (waited < 10_000 && (await live.Client.GetRecordStatus()).OutputActive)
                {
                    await Task.Delay(250);
                    waited += 250;
                }

                output.WriteLine($"OBS Studio went on saying that the recording ran for {waited} ms after it said that it stopped.");
                Assert.True(waited < 10_000, "OBS Studio still says that the recording runs.");
                Assert.Equal(ObsOutputStatus.Stopped, record.Status);
                Assert.True(Directory.EnumerateFiles(folder).Any(), "OBS Studio wrote no file.");
            }
            finally
            {
                // Whatever happened: the recording is stopped, the settings are as they were, and the file is gone.
                try
                {
                    await live.Client.StopRecord();
                }
                catch (ObsResponseException)
                {
                    // It does not run: it was stopped by the test.
                }

                for (var attempt = 0; attempt < 40 && (await live.Client.GetRecordStatus()).OutputActive; attempt++)
                {
                    await Task.Delay(250);
                }

                foreach (var (category, name, value) in before)
                {
                    await live.Client.SetProfileParameter(category, name, value);
                }

                for (var attempt = 0; attempt < 20 && Directory.Exists(folder); attempt++)
                {
                    try
                    {
                        Directory.Delete(folder, recursive: true);
                    }
                    catch (IOException)
                    {
                        // OBS Studio still has the file open for a moment.
                        await Task.Delay(250);
                    }
                }
            }

            var expected = paused
                ? new[] { ObsOutputStatus.Starting, ObsOutputStatus.Started, ObsOutputStatus.Paused, ObsOutputStatus.Started, ObsOutputStatus.Stopping, ObsOutputStatus.Stopped }
                : new[] { ObsOutputStatus.Starting, ObsOutputStatus.Started, ObsOutputStatus.Stopping, ObsOutputStatus.Stopped };

            // The changes are raised on a thread of their own, a moment after the model shows them.
            Assert.True(
                await AsyncTestHelpers.WaitUntilAsync(
                    () =>
                    {
                        lock (steps)
                        {
                            return steps.Count >= expected.Length;
                        }
                    },
                    Wait),
                "The model did not tell every step.");
            lock (steps)
            {
                output.WriteLine($"The steps of the recording, as the model showed them: {string.Join(" > ", steps)}.");
                Assert.Equal(expected, steps);
            }
        }
    }
}
