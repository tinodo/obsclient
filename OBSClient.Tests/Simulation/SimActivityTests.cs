namespace OBSStudioClient.Tests.Simulation
{
    /// <summary>
    /// Whether the inputs are on the air and shown, in the simulation: what <c>GetSourceActive</c> answers, and which events say it and in which order, as OBS Studio 33.0.0 did (measured by LiveActivityTests): the
    /// program scene, a transition, nested scenes, scene items that are hidden or removed, studio mode, and the inputs that OBS Studio destroys (it says nothing of them).
    /// </summary>
    public class SimActivityTests
    {
        private static readonly SimQuirks Ideal = SimQuirks.Ideal;
        private static readonly SimQuirks Fade = SimQuirks.Ideal with { TransitionDuration = TimeSpan.FromMilliseconds(300) };

        // The events in the log as short texts: the activity of an input says which input, and what it is now.
        private static string[] Events(SimHarness h)
        {
            h.Take();
            return h.Log.Where(entry => entry.IsEvent).Select(entry => entry.Name switch
            {
                "InputShowStateChanged" => $"show {entry.String("inputName")} {(entry.Bool("videoShowing") ? "true" : "false")}",
                "InputActiveStateChanged" => $"active {entry.String("inputName")} {(entry.Bool("videoActive") ? "true" : "false")}",
                _ => entry.Name,
            }).ToArray();
        }

        private static (bool Active, bool Showing) State(SimHarness h, Guid source)
        {
            var answer = h.Ok("GetSourceActive", new { sourceUuid = source.ToString() });
            return (answer.Bool("videoActive"), answer.Bool("videoShowing"));
        }

        private static void Switch(SimHarness h, Guid scene)
        {
            h.Ok("SetCurrentProgramScene", new { sceneUuid = scene.ToString() });
        }

        private static void StudioMode(SimHarness h, bool enabled)
        {
            h.Ok("SetStudioModeEnabled", new { studioModeEnabled = enabled });
        }

        private static void Preview(SimHarness h, Guid scene)
        {
            h.Ok("SetCurrentPreviewScene", new { sceneUuid = scene.ToString() });
        }

        private static void Enable(SimHarness h, Guid scene, int item, bool enabled)
        {
            h.Ok("SetSceneItemEnabled", new { sceneUuid = scene.ToString(), sceneItemId = item, sceneItemEnabled = enabled });
        }

        [Fact]
        public void AnInputOfASceneThatIsNotOnTheAir_IsNeitherOnTheAirNorShown_AndNothingIsSaidOfIt()
        {
            var h = new SimHarness(Ideal);
            var a = h.CreateScene("A");
            var (camera, _) = h.CreateInput(a, "Camera");
            h.Settle();

            Assert.Equal((false, false), State(h, camera));
            Assert.DoesNotContain(Events(h), text => text.StartsWith("show ", StringComparison.Ordinal) || text.StartsWith("active ", StringComparison.Ordinal));
        }

        [Fact]
        public void AnInputThatIsCreatedInTheProgramScene_IsShownAndOnTheAir_BeforeItsItemIsSelected()
        {
            var h = new SimHarness(Ideal);
            h.ClearLog();

            var (camera, _) = h.CreateInput(h.InitialScene, "Camera");
            h.Settle();

            Assert.Equal(["InputCreated", "SceneItemCreated", "show Camera true", "active Camera true", "SceneItemSelected"], Events(h));
            Assert.Equal((true, true), State(h, camera));
        }

        [Fact]
        public void AnInputThatIsPutInTheProgramScene_IsShownAndOnTheAir_AfterItsItemIsSelected()
        {
            var h = new SimHarness(Ideal);
            var a = h.CreateScene("A");
            var (camera, _) = h.CreateInput(a, "Camera");
            h.Settle();
            h.ClearLog();

            h.CreateSceneItem(h.InitialScene, camera);
            h.Settle();

            Assert.Equal(["SceneItemCreated", "SceneItemSelected", "show Camera true", "active Camera true"], Events(h));
            Assert.Equal((true, true), State(h, camera));
        }

        [Fact]
        public void AnItemThatIsCreatedHidden_PutsItsInputNeitherOnTheAirNorOnShow()
        {
            var h = new SimHarness(Ideal);
            h.ClearLog();

            var (camera, _) = h.CreateInput(h.InitialScene, "Camera");
            h.Ok("CreateSceneItem", new { sceneUuid = h.InitialScene.ToString(), sourceUuid = camera.ToString(), sceneItemEnabled = false });
            h.Settle();

            // The second item is hidden, and the input is shown by the first one.
            Assert.Equal((true, true), State(h, camera));
        }

        [Fact]
        public void ACutToAnotherScene_PutsItsInputsOnTheAirWhenTheTransitionStarts_AndTakesTheOldOnesOffAfterItEnds()
        {
            var h = new SimHarness(Ideal);
            var (logo, _) = h.CreateInput(h.InitialScene, "Logo");
            var a = h.CreateScene("A");
            var (camera, _) = h.CreateInput(a, "Camera");
            h.Settle();
            h.ClearLog();

            Switch(h, a);
            h.Settle();

            Assert.Equal(
                ["SceneTransitionStarted", "show Camera true", "active Camera true", "SceneTransitionVideoEnded", "CurrentProgramSceneChanged", "SceneTransitionEnded", "show Logo false", "active Logo false"],
                Events(h));
            Assert.Equal((true, true), State(h, camera));
            Assert.Equal((false, false), State(h, logo));
        }

        [Fact]
        public void AFade_KeepsBothScenesOnTheAir_WhileItRuns()
        {
            var h = new SimHarness(Fade);
            var (logo, _) = h.CreateInput(h.InitialScene, "Logo");
            var a = h.CreateScene("A");
            var (camera, _) = h.CreateInput(a, "Camera");
            h.Settle();
            h.ClearLog();

            Switch(h, a);
            h.Run(100);

            Assert.Equal((true, true), State(h, camera));
            Assert.Equal((true, true), State(h, logo));
            Assert.Equal(["SceneTransitionStarted", "show Camera true", "active Camera true"], Events(h));

            h.Settle();

            Assert.Equal((true, true), State(h, camera));
            Assert.Equal((false, false), State(h, logo));
            Assert.Equal(["SceneTransitionStarted", "show Camera true", "active Camera true", "SceneTransitionVideoEnded", "CurrentProgramSceneChanged", "SceneTransitionEnded", "show Logo false", "active Logo false"], Events(h));
        }

        [Fact]
        public void ASecondSwitchWhileAFadeRuns_TakesTheFirstSceneOffTheAir_AndKeepsTheSecondOnItUntilTheEnd()
        {
            var h = new SimHarness(Fade);
            var a = h.CreateScene("A");
            var b = h.CreateScene("B");
            var (alpha, _) = h.CreateInput(a, "Alpha");
            var (beta, _) = h.CreateInput(b, "Beta");
            h.Settle();

            Switch(h, a);
            h.Run(100);
            Switch(h, b);
            h.Run(100);

            // A was on the air for the first transition, and is not for the second, which comes from A: the program scene that the first went to.
            Assert.Equal((true, true), State(h, alpha));
            Assert.Equal((true, true), State(h, beta));

            h.Settle();

            Assert.Equal((false, false), State(h, alpha));
            Assert.Equal((true, true), State(h, beta));
        }

        [Fact]
        public void ANestedScene_PutsItsInputsOnTheAir_WhileTheItemThatShowsItIsEnabled()
        {
            var h = new SimHarness(Ideal);
            var nested = h.CreateScene("Nested");
            var (camera, _) = h.CreateInput(nested, "Camera");
            var nesting = h.CreateSceneItem(h.InitialScene, nested);
            h.Settle();
            Assert.Equal((true, true), State(h, camera));
            h.ClearLog();

            Enable(h, h.InitialScene, nesting, false);
            h.Settle();

            // The change of the item comes first, and then what it does to the input.
            Assert.Equal(["SceneItemEnableStateChanged", "show Camera false", "active Camera false"], Events(h));
            Assert.Equal((false, false), State(h, camera));

            h.ClearLog();
            Enable(h, h.InitialScene, nesting, true);
            h.Settle();

            Assert.Equal(["SceneItemEnableStateChanged", "show Camera true", "active Camera true"], Events(h));
        }

        [Fact]
        public void AnInputThatIsReachedInTwoWays_StaysOnTheAir_WhenOneOfThemEnds()
        {
            var h = new SimHarness(Ideal);
            var nested = h.CreateScene("Nested");
            var (camera, _) = h.CreateInput(nested, "Camera");
            var nesting = h.CreateSceneItem(h.InitialScene, nested);
            h.CreateSceneItem(h.InitialScene, camera);
            h.Settle();
            h.ClearLog();

            Enable(h, h.InitialScene, nesting, false);
            h.Settle();

            Assert.Equal(["SceneItemEnableStateChanged"], Events(h));
            Assert.Equal((true, true), State(h, camera));
        }

        [Fact]
        public void HidingAndShowingAnItemOfTheProgramScene_TakesItsInputOffTheAirAndPutsItBack()
        {
            var h = new SimHarness(Ideal);
            var (camera, item) = h.CreateInput(h.InitialScene, "Camera");
            h.Settle();
            h.ClearLog();

            Enable(h, h.InitialScene, item, false);
            h.Settle();
            Assert.Equal(["SceneItemEnableStateChanged", "show Camera false", "active Camera false"], Events(h));
            Assert.Equal((false, false), State(h, camera));

            h.ClearLog();
            Enable(h, h.InitialScene, item, true);
            h.Settle();
            Assert.Equal(["SceneItemEnableStateChanged", "show Camera true", "active Camera true"], Events(h));
            Assert.Equal((true, true), State(h, camera));
        }

        [Fact]
        public void RemovingAnItemOfAnInputThatLivesOn_TakesItOffTheAirAfterTheItemIsRemoved()
        {
            var h = new SimHarness(Ideal);
            var a = h.CreateScene("A");
            var (camera, _) = h.CreateInput(a, "Camera");
            var item = h.CreateSceneItem(h.InitialScene, camera);
            h.Settle();
            h.ClearLog();

            h.Ok("RemoveSceneItem", new { sceneUuid = h.InitialScene.ToString(), sceneItemId = item });
            h.Settle();

            Assert.Equal(["SceneItemRemoved", "show Camera false", "active Camera false"], Events(h));
            Assert.Equal((false, false), State(h, camera));
        }

        [Fact]
        public void AnInputThatOBSStudioDestroys_IsNotSaidToGoOffTheAir()
        {
            var h = new SimHarness(Ideal);
            var (camera, item) = h.CreateInput(h.InitialScene, "Camera");
            h.Settle();
            h.ClearLog();

            h.Ok("RemoveSceneItem", new { sceneUuid = h.InitialScene.ToString(), sceneItemId = item });
            h.Settle();

            var events = Events(h);
            Assert.Equal(["SceneItemRemoved", "InputRemoved"], events);
            Assert.Equal(600, h.Call("GetSourceActive", new { sourceUuid = camera.ToString() }).Code);
        }

        [Fact]
        public void AnInputThatIsRemoved_IsNotSaidToGoOffTheAirEither()
        {
            var h = new SimHarness(Ideal);
            var (camera, _) = h.CreateInput(h.InitialScene, "Camera");
            h.Settle();
            h.ClearLog();

            h.Ok("RemoveInput", new { inputUuid = camera.ToString() });
            h.Settle();

            Assert.DoesNotContain(Events(h), text => text.StartsWith("show ", StringComparison.Ordinal) || text.StartsWith("active ", StringComparison.Ordinal));
        }

        [Fact]
        public void RemovingTheProgramScene_PutsTheInputsOfTheSceneThatTakesItsPlaceOnTheAir()
        {
            var h = new SimHarness(Ideal);
            h.CreateInput(h.InitialScene, "Logo");
            var a = h.CreateScene("A");
            var (camera, _) = h.CreateInput(a, "Camera");
            h.Settle();
            h.ClearLog();

            h.Ok("RemoveScene", new { sceneUuid = h.InitialScene.ToString() });
            h.Settle();

            var events = Events(h);
            Assert.Equal(["SceneRemoved", "CurrentProgramSceneChanged"], events.Take(2).ToArray());
            Assert.Contains("show Camera true", events);
            Assert.Contains("active Camera true", events);

            // The input of the scene that went was destroyed with it, and nothing is said of that.
            Assert.DoesNotContain(events, text => text.Contains("Logo", StringComparison.Ordinal) && (text.StartsWith("show ", StringComparison.Ordinal) || text.StartsWith("active ", StringComparison.Ordinal)));
            Assert.Equal((true, true), State(h, camera));
        }

        [Fact]
        public void SwitchingStudioModeOn_SaysNothingOfTheActivity()
        {
            var h = new SimHarness(Ideal);
            var (camera, _) = h.CreateInput(h.InitialScene, "Camera");
            h.Settle();
            h.ClearLog();

            StudioMode(h, true);
            h.Settle();

            Assert.Equal(["StudioModeStateChanged"], Events(h));
            Assert.Equal((true, true), State(h, camera));
        }

        [Fact]
        public void ThePreviewScene_ShowsItsInputs_WithoutPuttingThemOnTheAir()
        {
            var h = new SimHarness(Ideal);
            var (camera, _) = h.CreateInput(h.InitialScene, "Camera");
            var a = h.CreateScene("A");
            var (logo, _) = h.CreateInput(a, "Logo");
            StudioMode(h, true);
            h.Settle();
            h.ClearLog();

            Preview(h, a);
            h.Settle();

            Assert.Equal(["CurrentPreviewSceneChanged", "show Logo true"], Events(h));
            Assert.Equal((false, true), State(h, logo));
            Assert.Equal((true, true), State(h, camera));

            h.ClearLog();
            Preview(h, h.InitialScene);
            h.Settle();

            Assert.Equal(["CurrentPreviewSceneChanged", "show Logo false"], Events(h));
            Assert.Equal((false, false), State(h, logo));
        }

        [Fact]
        public void TheTransitionOfStudioMode_PutsThePreviewOnTheAirAtOnce_AndTakesTheOldProgramSceneOffTheAirAfterItEnds()
        {
            var h = new SimHarness(Ideal);
            var (camera, _) = h.CreateInput(h.InitialScene, "Camera");
            var a = h.CreateScene("A");
            var (logo, _) = h.CreateInput(a, "Logo");
            StudioMode(h, true);
            Preview(h, a);
            h.Settle();
            h.ClearLog();

            h.Ok("TriggerStudioModeTransition");
            h.Settle();

            // The logo was shown by the preview, so it only goes on the air. The camera stays shown: the scene that was on the air is the preview scene now.
            Assert.Equal(
                ["SceneTransitionStarted", "active Logo true", "SceneTransitionVideoEnded", "CurrentPreviewSceneChanged", "CurrentProgramSceneChanged", "SceneTransitionEnded", "active Camera false"],
                Events(h));
            Assert.Equal((true, true), State(h, logo));
            Assert.Equal((false, true), State(h, camera));
        }

        [Fact]
        public void SwitchingStudioModeOff_StopsShowingWhatOnlyThePreviewShowed()
        {
            var h = new SimHarness(Ideal);
            h.CreateInput(h.InitialScene, "Camera");
            var a = h.CreateScene("A");
            var (logo, _) = h.CreateInput(a, "Logo");
            StudioMode(h, true);
            Preview(h, a);
            h.Settle();
            Assert.Equal((false, true), State(h, logo));
            h.ClearLog();

            StudioMode(h, false);
            h.Settle();

            Assert.Equal(["CurrentProgramSceneChanged", "CurrentProgramSceneChanged", "StudioModeStateChanged", "show Logo false"], Events(h));
            Assert.Equal((false, false), State(h, logo));
        }

        [Fact]
        public void HidingAnItemOfTheProgramScene_InStudioMode_ChangesNothingOfItsInput_UntilStudioModeIsSwitchedOff()
        {
            // Studio mode puts a copy of the program scene on the air, so what is done to the scene is not done to what is on the air (measured with OBS Studio 33.0.0).
            var h = new SimHarness(Ideal);
            var (camera, item) = h.CreateInput(h.InitialScene, "Camera");
            StudioMode(h, true);
            h.Settle();
            h.ClearLog();

            Enable(h, h.InitialScene, item, false);
            h.Settle();

            Assert.Equal(["SceneItemEnableStateChanged"], Events(h));
            Assert.Equal((true, true), State(h, camera));

            h.ClearLog();
            StudioMode(h, false);
            h.Settle();

            Assert.Equal(["CurrentProgramSceneChanged", "CurrentProgramSceneChanged", "StudioModeStateChanged", "show Camera false", "active Camera false"], Events(h));
            Assert.Equal((false, false), State(h, camera));
        }

        [Fact]
        public void ShowingAnItemOfTheProgramScene_InStudioMode_ShowsItsInputInThePreview_ButDoesNotPutItOnTheAir()
        {
            // Measured with OBS Studio 33.0.0 (LiveActivityTests, steps 20c and 20d): the copy that is on the air does not have what is done to the scene afterwards, and the preview scene is the scene itself.
            var h = new SimHarness(Ideal);
            var (camera, item) = h.CreateInput(h.InitialScene, "Camera");
            Enable(h, h.InitialScene, item, false);
            StudioMode(h, true);
            h.Settle();
            Assert.Equal((false, false), State(h, camera));
            h.ClearLog();

            Enable(h, h.InitialScene, item, true);
            h.Settle();

            Assert.Equal(["SceneItemEnableStateChanged", "show Camera true"], Events(h));
            Assert.Equal((false, true), State(h, camera));
        }

        [Fact]
        public void AnInputThatIsCreatedInTheProgramScene_InStudioMode_IsShownInThePreview_ButNotOnTheAir()
        {
            // Measured with OBS Studio 33.0.0 (LiveActivityTests, step 20e): the copy that is on the air does not hold an item that is made after it was.
            var h = new SimHarness(Ideal);
            StudioMode(h, true);
            h.Settle();
            h.ClearLog();

            var (camera, _) = h.CreateInput(h.InitialScene, "Camera");
            h.Settle();

            Assert.Equal(["InputCreated", "SceneItemCreated", "show Camera true", "SceneItemSelected"], Events(h));
            Assert.Equal((false, true), State(h, camera));
        }

        [Fact]
        public void ASceneThatIsLoadedWithACollection_HasItsInputsOnTheAir_WithoutAnyEventForIt()
        {
            var h = new SimHarness(Ideal);
            var (camera, _) = h.CreateInput(h.InitialScene, "Camera");
            h.Settle();
            h.Ok("CreateSceneCollection", new { sceneCollectionName = "Other" });
            h.Settle();
            h.ClearLog();

            h.Ok("SetCurrentSceneCollection", new { sceneCollectionName = "Untitled" });
            h.Settle();

            Assert.DoesNotContain(Events(h), text => text.StartsWith("show ", StringComparison.Ordinal) || text.StartsWith("active ", StringComparison.Ordinal));
            Assert.Equal((true, true), State(h, camera));
        }

        [Fact]
        public void AScene_SaysWhatReachesIt_AndTheProgramSceneIsNotOnTheAirInStudioMode()
        {
            var h = new SimHarness(Ideal);
            var nested = h.CreateScene("Nested");
            var other = h.CreateScene("Other");
            h.CreateSceneItem(h.InitialScene, nested);
            h.Settle();

            Assert.Equal((true, true), State(h, h.InitialScene));
            Assert.Equal((true, true), State(h, nested));
            Assert.Equal((false, false), State(h, other));

            // OBS Studio 33.0.0 said odd things of scenes in studio mode: the program scene was not on the air, and the preview scene was only shown.
            StudioMode(h, true);
            Preview(h, other);
            h.Settle();

            Assert.Equal((false, false), State(h, h.InitialScene));
            Assert.Equal((true, true), State(h, nested));
            Assert.Equal((false, true), State(h, other));
        }

        [Fact]
        public void GetSourceActive_FindsTheSourceByNameOrUuid_AndRefusesWhatIsNotThere()
        {
            var h = new SimHarness(Ideal);
            var (camera, _) = h.CreateInput(h.InitialScene, "Camera");
            h.Settle();

            var byName = h.Ok("GetSourceActive", new { sourceName = "Camera" });
            var byUuid = h.Ok("GetSourceActive", new { sourceUuid = camera.ToString() });

            Assert.Equal(["videoActive", "videoShowing"], byName.Data.EnumerateObject().Select(member => member.Name).ToArray());
            Assert.True(byName.Bool("videoActive"));
            Assert.True(byUuid.Bool("videoShowing"));
            Assert.Equal(600, h.Call("GetSourceActive", new { sourceName = "No such source" }).Code);
            Assert.Equal(600, h.Call("GetSourceActive", new { sourceUuid = Guid.NewGuid().ToString() }).Code);
            Assert.Equal(300, h.Call("GetSourceActive", new { }).Code);
        }
    }
}
