namespace OBSStudioClient.Tests.Simulation
{
    using System.Text.Json;

    /// <summary>
    /// Studio mode, the preview scene and the transitions of the simulation, as OBS Studio 33.0.0 does them (measured by LiveTransitionTests): what is announced
    /// and in which order, what is refused and how, and what is read at once while the events come later.
    /// </summary>
    public class SimStudioModeTests
    {
        private static readonly SimQuirks Ideal = SimQuirks.Ideal;
        private static readonly SimQuirks Fade = SimQuirks.Ideal with { TransitionDuration = TimeSpan.FromMilliseconds(300) };

        private static string[] MemberNames(SimEntry entry) => entry.Data.EnumerateObject().Select(member => member.Name).ToArray();

        private static void StudioMode(SimHarness h, bool enabled)
        {
            h.Ok("SetStudioModeEnabled", new { studioModeEnabled = enabled });
        }

        [Fact]
        public void StudioModeIsOffToBeginWith_AndWhatNeedsItIsRefusedWithoutAComment()
        {
            var h = new SimHarness(Ideal);
            var scene = h.CreateScene("A");

            Assert.False(h.Ok("GetStudioModeEnabled").Bool("studioModeEnabled"));
            foreach (var (type, data) in new (string, object?)[] { ("GetCurrentPreviewScene", null), ("SetCurrentPreviewScene", new { sceneUuid = scene.ToString() }), ("TriggerStudioModeTransition", null) })
            {
                var refusal = h.Call(type, data);
                Assert.Equal(506, refusal.Code);
                Assert.Null(refusal.Comment);
            }

            var list = h.Ok("GetSceneList");
            Assert.Equal(JsonValueKind.Null, list.Data.GetProperty("currentPreviewSceneName").ValueKind);
            Assert.Equal(JsonValueKind.Null, list.Data.GetProperty("currentPreviewSceneUuid").ValueKind);
        }

        [Fact]
        public void SwitchingStudioModeOn_MakesTheProgramSceneThePreviewScene_WithoutAnEventForIt()
        {
            var h = new SimHarness(Ideal);
            var other = h.CreateScene("Other");
            h.Ok("SetCurrentProgramScene", new { sceneUuid = other.ToString() });
            h.Settle();
            h.ClearLog();

            StudioMode(h, true);
            h.Settle();

            Assert.Equal(["StudioModeStateChanged"], h.EventNames());
            Assert.True(h.Events().Single().Bool("studioModeEnabled"));
            Assert.True(h.Ok("GetStudioModeEnabled").Bool("studioModeEnabled"));
            var preview = h.Ok("GetCurrentPreviewScene");
            Assert.Equal(["currentPreviewSceneName", "currentPreviewSceneUuid", "sceneName", "sceneUuid"], MemberNames(preview).Order(StringComparer.Ordinal).ToArray());
            Assert.Equal(other, preview.Uuid("sceneUuid"));
            Assert.Equal(other, preview.Uuid("currentPreviewSceneUuid"));
            Assert.Equal("Other", preview.String("currentPreviewSceneName"));
            Assert.Equal(other.ToString(), h.Ok("GetSceneList").String("currentPreviewSceneUuid"));

            // Switching it on again is not announced.
            h.ClearLog();
            StudioMode(h, true);
            h.Settle();
            Assert.Empty(h.EventNames());
        }

        [Fact]
        public void SwitchingStudioModeOff_AnnouncesTheProgramSceneTwice_AndThenThatStudioModeIsOff()
        {
            var h = new SimHarness(Ideal);
            StudioMode(h, true);
            h.Settle();
            h.ClearLog();

            StudioMode(h, false);
            h.Settle();

            Assert.Equal(["CurrentProgramSceneChanged", "CurrentProgramSceneChanged", "StudioModeStateChanged"], h.EventNames());
            Assert.False(h.Events("StudioModeStateChanged").Single().Bool("studioModeEnabled"));
            Assert.False(h.Ok("GetStudioModeEnabled").Bool("studioModeEnabled"));
            Assert.Equal(506, h.Call("GetCurrentPreviewScene").Code);
        }

        [Fact]
        public void SwitchingStudioModeOff_AnnouncesThePreviewSceneFirst_WhenItIsNotTheProgramScene()
        {
            // Measured with OBS Studio 33.0.0 (LiveActivityTests): the preview scene is the program scene for a moment, and then the scene that was the program scene is again.
            var h = new SimHarness(Ideal);
            var a = h.CreateScene("A");
            StudioMode(h, true);
            h.Ok("SetCurrentPreviewScene", new { sceneUuid = a.ToString() });
            h.Settle();
            h.ClearLog();

            StudioMode(h, false);
            h.Settle();

            Assert.Equal(["CurrentProgramSceneChanged", "CurrentProgramSceneChanged", "StudioModeStateChanged"], h.EventNames());
            var announced = h.Events("CurrentProgramSceneChanged");
            Assert.Equal(a, announced[0].Uuid("sceneUuid"));
            Assert.Equal(h.InitialScene, announced[1].Uuid("sceneUuid"));
            Assert.Equal(h.InitialScene, h.Ok("GetCurrentProgramScene").Uuid("sceneUuid"));
        }

        [Fact]
        public void ThePreviewScene_IsSetByNameOrUuid_AndAnnouncedOnlyWhenItChanges()
        {
            var h = new SimHarness(Ideal);
            var a = h.CreateScene("A");
            StudioMode(h, true);
            h.Settle();
            h.ClearLog();

            h.Ok("SetCurrentPreviewScene", new { sceneName = "A" });
            h.Settle();

            Assert.Equal(["CurrentPreviewSceneChanged"], h.EventNames());
            var changed = h.Events().Single();
            Assert.Equal(["sceneName", "sceneUuid"], MemberNames(changed));
            Assert.Equal(a, changed.Uuid("sceneUuid"));
            Assert.Equal(a, h.Ok("GetCurrentPreviewScene").Uuid("sceneUuid"));
            Assert.Equal(h.InitialScene, h.Ok("GetCurrentProgramScene").Uuid("sceneUuid"));

            h.ClearLog();
            h.Ok("SetCurrentPreviewScene", new { sceneUuid = a.ToString() });
            h.Settle();
            Assert.Empty(h.EventNames());

            Assert.Equal(600, h.Call("SetCurrentPreviewScene", new { sceneName = "No such scene" }).Code);
        }

        [Fact]
        public void ARemovedPreviewScene_IsReplacedByTheSceneThatIsFirstInTheList_WhichIsNotTheProgramScene()
        {
            var h = new SimHarness(Ideal);
            var a = h.CreateScene("A");
            var b = h.CreateScene("B");
            StudioMode(h, true);
            h.Ok("SetCurrentPreviewScene", new { sceneUuid = b.ToString() });
            h.Settle();
            h.ClearLog();

            h.Ok("RemoveScene", new { sceneUuid = b.ToString() });
            h.Settle();

            Assert.Equal(["SceneRemoved", "CurrentPreviewSceneChanged", "SceneListChanged"], h.EventNames());
            Assert.Equal(a, h.Events("CurrentPreviewSceneChanged").Single().Uuid("sceneUuid"));
            Assert.Equal(a, h.Ok("GetCurrentPreviewScene").Uuid("sceneUuid"));
            Assert.Equal(h.InitialScene, h.Ok("GetCurrentProgramScene").Uuid("sceneUuid"));
        }

        [Fact]
        public void TheTransitionOfStudioMode_PutsThePreviewSceneOnTheAirAtOnce_AndSwapsThemWhenItEnds()
        {
            var h = new SimHarness(Fade);
            var a = h.CreateScene("A");
            var before = h.InitialScene;
            StudioMode(h, true);
            h.Ok("SetCurrentPreviewScene", new { sceneUuid = a.ToString() });
            h.Settle();
            h.ClearLog();

            h.Ok("TriggerStudioModeTransition");

            Assert.Equal(a, h.Ok("GetCurrentProgramScene").Uuid("sceneUuid"));
            Assert.Equal(a, h.Ok("GetCurrentPreviewScene").Uuid("sceneUuid"));
            Assert.Equal(["SceneTransitionStarted"], h.EventNames());

            // A transition that is running is not started again, and nothing is announced for the request.
            h.Ok("TriggerStudioModeTransition");
            h.Run(299);
            Assert.Equal(["SceneTransitionStarted"], h.EventNames());
            h.Run(2);

            Assert.Equal(["SceneTransitionStarted", "SceneTransitionVideoEnded", "CurrentPreviewSceneChanged", "CurrentProgramSceneChanged", "SceneTransitionEnded"], h.EventNames());
            Assert.Equal(before, h.Events("CurrentPreviewSceneChanged").Single().Uuid("sceneUuid"));
            Assert.Equal(a, h.Events("CurrentProgramSceneChanged").Single().Uuid("sceneUuid"));
            Assert.Equal(before, h.Ok("GetCurrentPreviewScene").Uuid("sceneUuid"));
            Assert.Equal(a, h.Ok("GetCurrentProgramScene").Uuid("sceneUuid"));
        }

        [Fact]
        public void ASceneThatWasRemovedWhileTheTransitionRan_IsNotMadeThePreviewScene_WhenItEnds()
        {
            // ASSUMPTION: not measured. The scene that was on the air goes to the preview when the transition ends, unless it was removed meanwhile: the user interface does not list it any more, and it is
            // not a scene that the next transition could put on the air.
            var h = new SimHarness(Fade);
            var a = h.CreateScene("A");
            var before = h.InitialScene;
            StudioMode(h, true);
            h.Ok("SetCurrentPreviewScene", new { sceneUuid = a.ToString() });
            h.Settle();
            h.Ok("TriggerStudioModeTransition");
            h.Run(100);
            h.Ok("RemoveScene", new { sceneUuid = before.ToString() });
            h.ClearLog();

            h.Settle();

            Assert.Equal(["SceneTransitionVideoEnded", "CurrentProgramSceneChanged", "SceneTransitionEnded"], h.EventNames());
            Assert.Equal(a, h.Ok("GetCurrentPreviewScene").Uuid("sceneUuid"));
            Assert.Equal(a, h.Ok("GetCurrentProgramScene").Uuid("sceneUuid"));
            h.Sim.CheckInvariants();
        }

        [Fact]
        public void ASwitchOfTheProgramScene_InStudioMode_LeavesThePreviewSceneAlone()
        {
            var h = new SimHarness(Ideal);
            var a = h.CreateScene("A");
            var b = h.CreateScene("B");
            StudioMode(h, true);
            h.Ok("SetCurrentPreviewScene", new { sceneUuid = a.ToString() });
            h.Settle();
            h.ClearLog();

            h.Ok("SetCurrentProgramScene", new { sceneUuid = b.ToString() });
            h.Settle();

            Assert.Equal(["SceneTransitionStarted", "SceneTransitionVideoEnded", "CurrentProgramSceneChanged", "SceneTransitionEnded"], h.EventNames());
            Assert.Equal(a, h.Ok("GetCurrentPreviewScene").Uuid("sceneUuid"));
            Assert.Equal(b, h.Ok("GetCurrentProgramScene").Uuid("sceneUuid"));
        }

        [Fact]
        public void TheCurrentTransition_IsSetByName_AndAnnouncedEveryTime()
        {
            var h = new SimHarness(Fade);

            var current = h.Ok("GetCurrentSceneTransition");
            Assert.Equal("Fade", current.String("transitionName"));
            Assert.Equal("fade_transition", current.String("transitionKind"));
            Assert.False(current.Bool("transitionFixed"));
            Assert.False(current.Bool("transitionConfigurable"));
            Assert.Equal(300, current.Int("transitionDuration"));
            Assert.Equal(JsonValueKind.Null, current.Data.GetProperty("transitionSettings").ValueKind);

            h.ClearLog();
            h.Ok("SetCurrentSceneTransition", new { transitionName = "Cut" });
            h.Ok("SetCurrentSceneTransition", new { transitionName = "Cut" });
            h.Settle();

            Assert.Equal(["CurrentSceneTransitionChanged", "CurrentSceneTransitionChanged"], h.EventNames());
            Assert.Equal("Cut", h.Events().First().String("transitionName"));
            var cut = h.Ok("GetCurrentSceneTransition");
            Assert.True(cut.Bool("transitionFixed"));
            Assert.Equal("cut_transition", cut.String("transitionKind"));
            Assert.Equal(JsonValueKind.Null, cut.Data.GetProperty("transitionDuration").ValueKind);

            var unknown = h.Call("SetCurrentSceneTransition", new { transitionName = "Spin" });
            Assert.Equal((600, "No scene transition was found by that name."), (unknown.Code, unknown.Comment));
        }

        [Fact]
        public void TheDurationOfTheTransitions_IsCheckedForItsRange_AndAnnouncedOnlyWhenItChanges()
        {
            var h = new SimHarness(Fade);
            h.ClearLog();

            Assert.Equal(402, h.Call("SetCurrentSceneTransitionDuration", new { transitionDuration = 49 }).Code);
            Assert.Equal(402, h.Call("SetCurrentSceneTransitionDuration", new { transitionDuration = 20001 }).Code);
            h.Ok("SetCurrentSceneTransitionDuration", new { transitionDuration = 300 });
            h.Settle();
            Assert.Empty(h.EventNames());

            h.Ok("SetCurrentSceneTransitionDuration", new { transitionDuration = 1200 });
            h.Settle();
            Assert.Equal(["CurrentSceneTransitionDurationChanged"], h.EventNames());
            Assert.Equal(1200, h.Events().Single().Int("transitionDuration"));
            Assert.Equal(1200, h.Ok("GetCurrentSceneTransition").Int("transitionDuration"));

            // The duration belongs to the transitions and not to the current one: it is set while the cut is current, and the fade has it.
            h.Ok("SetCurrentSceneTransition", new { transitionName = "Cut" });
            h.Ok("SetCurrentSceneTransitionDuration", new { transitionDuration = 700 });
            h.Ok("SetCurrentSceneTransition", new { transitionName = "Fade" });
            Assert.Equal(700, h.Ok("GetCurrentSceneTransition").Int("transitionDuration"));
        }
    }
}
