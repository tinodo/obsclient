namespace OBSStudioClient.Tests.Model
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Model.Engine;

    /// <summary>
    /// What the model holds about studio mode, the preview scene and the scene transitions: what is read when (the preview scene only while studio mode is on, because OBS Studio refuses to say
    /// it otherwise, and because it sends no event when it makes the program scene the preview scene), what the events make of it, and when a transition is running (as OBS Studio 33.0.0 was
    /// measured to do it: see LiveTransitionTests).
    /// </summary>
    public class ModelEngineTransitionTests
    {
        private const int Transitions = 16;
        private const int Scenes = 4;
        private const int Ui = 1024;
        private const string StingerKind = "obs_stinger_transition";

        private static EngineHarness Started(Action<FakeWorld>? setup = null)
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            setup?.Invoke(h.World);
            h.Start();
            h.SettleWithTime();
            h.Engine.TakeChanges();
            return h;
        }

        // Settles the engine, and returns what it asked.
        private static List<ReadRequest> Settle(EngineHarness h)
        {
            var asked = new List<ReadRequest>();
            h.SettleWithTime(request =>
            {
                asked.Add(request);
                return h.World.Answer(request);
            });
            return asked;
        }

        private static ModelChange[] Changes(EngineHarness h, ModelChangeKind kind)
        {
            return h.Engine.TakeChanges().Where(change => change.Kind == kind).ToArray();
        }

        private static ReadRequest TheRead(EngineHarness h)
        {
            var ticket = h.Take();
            Assert.NotNull(ticket);
            var request = Assert.Single(ticket.Requests);
            h.Complete(ticket);
            return request;
        }

        private static void Started(EngineHarness h, string name, Guid uuid)
        {
            h.Event("SceneTransitionStarted", new { transitionName = name, transitionUuid = uuid }, Transitions);
        }

        private static void Ended(EngineHarness h, string name, Guid uuid)
        {
            h.Event("SceneTransitionEnded", new { transitionName = name, transitionUuid = uuid }, Transitions);
        }

        private static void VideoEnded(EngineHarness h, string name, Guid uuid)
        {
            h.Event("SceneTransitionVideoEnded", new { transitionName = name, transitionUuid = uuid }, Transitions);
        }

        [Fact]
        public void TheCurrentTransition_IsReadWithTheFirstReads_AndItsDurationWithIt()
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            h.Start();

            var first = h.Take()!;
            Assert.Contains(first.Requests, request => request.Kind == ReadKind.Transition && request.Type == RequestType.GetCurrentSceneTransition);
            Assert.DoesNotContain(first.Requests, request => request.Kind == ReadKind.Preview);
            h.Complete(first);
            h.SettleWithTime();

            var snapshot = h.Engine.Snapshot();
            Assert.Equal(("Fade", "fade_transition", false, false), (snapshot.Transition!.Name, snapshot.Transition.Kind, snapshot.Transition.IsFixed, snapshot.Transition.IsConfigurable));
            Assert.Equal(300, snapshot.TransitionDuration);
            Assert.False(snapshot.TransitionRunning);
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void ACut_SaysNoDuration_SoTheModelKnowsNone_UntilItIsTold()
        {
            var h = Started(world => world.Transition = ("Cut", Guid.NewGuid(), "cut_transition", true, false));

            Assert.True(h.Engine.Snapshot().Transition!.IsFixed);
            Assert.Null(h.Engine.Snapshot().TransitionDuration);

            h.Event("CurrentSceneTransitionDurationChanged", new { transitionDuration = 700 }, Transitions);

            Assert.Equal(700, h.Engine.Snapshot().TransitionDuration);
            var change = Assert.Single(Changes(h, ModelChangeKind.TransitionDurationChanged));
            Assert.Null(change.OldValue);
            Assert.Equal(700, change.NewValue);
        }

        [Fact]
        public void TheDurationThatAnEventSays_IsOneChange_AndTheSameDurationAgainIsNone()
        {
            var h = Started();

            h.Event("CurrentSceneTransitionDurationChanged", new { transitionDuration = 1200 }, Transitions);
            h.Event("CurrentSceneTransitionDurationChanged", new { transitionDuration = 1200 }, Transitions);

            var change = Assert.Single(Changes(h, ModelChangeKind.TransitionDurationChanged));
            Assert.Equal((300, 1200), ((int)change.OldValue!, (int)change.NewValue!));
        }

        [Fact]
        public void TheEventAboutTheCurrentTransition_IsFollowedByARead_AndTheModelHoldsWhatTheReadSays()
        {
            var h = Started();
            var cut = Guid.NewGuid();
            h.World.Transition = ("Cut", cut, "cut_transition", true, false);

            h.Event("CurrentSceneTransitionChanged", new { transitionName = "Cut", transitionUuid = cut }, Transitions);

            // The payload is not taken: what the model holds changes when the read says so.
            Assert.Equal("Fade", h.Engine.Snapshot().Transition!.Name);
            Assert.Equal(ReadKind.Transition, TheRead(h).Kind);
            var snapshot = h.Engine.Snapshot();
            Assert.Equal(("Cut", cut, true), (snapshot.Transition!.Name, snapshot.Transition.Uuid, snapshot.Transition.IsFixed));
            var change = Assert.Single(Changes(h, ModelChangeKind.TransitionChanged));
            Assert.Equal("Cut", change.Text);
            Assert.Equal("Fade", ((TransitionInfo)change.OldValue!).Name);
        }

        [Fact]
        public void ASceneCollectionWithoutATransition_IsKnownToHaveNone_WhenOBSStudioRefusesThreeTimesInARow()
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            h.World.Transition = null;
            h.Start();

            var asked = Settle(h);

            Assert.Equal(3, asked.Count(request => request.Kind == ReadKind.Transition));
            Assert.Null(h.Engine.Snapshot().Transition);
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void ARefusalToSayTheCurrentTransition_IsAskedAgain_BecauseItCanBeAMomentInWhichOBSStudioHoldsNone()
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            h.Start();
            var refusals = 0;

            h.SettleWithTime(request =>
            {
                if (request.Kind == ReadKind.Transition && refusals < 2)
                {
                    refusals++;
                    return (702, null);
                }

                return h.World.Answer(request);
            });

            Assert.Equal(2, refusals);
            Assert.Equal("Fade", h.Engine.Snapshot().Transition!.Name);
            Assert.Equal(300, h.Engine.Snapshot().TransitionDuration);
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void WhileStudioModeIsOff_ThereIsNoPreviewScene_AndNothingIsAskedAboutOne()
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            h.Start();

            var asked = Settle(h);

            Assert.DoesNotContain(asked, request => request.Kind == ReadKind.Preview);
            var snapshot = h.Engine.Snapshot();
            Assert.False(snapshot.StudioMode);
            Assert.Null(snapshot.PreviewSceneUuid);
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void WithStudioModeOn_ThePreviewSceneIsReadOnceStudioModeIsKnown()
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            var gaming = h.World.AddScene("Gaming");
            h.World.StudioMode = true;
            h.World.Preview = gaming;
            h.Start();

            var asked = Settle(h);

            // OBS Studio refuses the request while studio mode is off: it is only sent when the model has read that it is on.
            Assert.Single(asked, request => request.Kind == ReadKind.Preview);
            Assert.True(asked.FindIndex(request => request.Kind == ReadKind.StudioMode) < asked.FindIndex(request => request.Kind == ReadKind.Preview));
            var snapshot = h.Engine.Snapshot();
            Assert.True(snapshot.StudioMode);
            Assert.Equal(gaming, snapshot.PreviewSceneUuid);
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void SwitchingStudioModeOn_IsFollowedByARead_OfThePreviewScene_BecauseNoEventSaysWhichItIs()
        {
            var h = Started();
            h.World.StudioMode = true;

            h.Event("StudioModeStateChanged", new { studioModeEnabled = true }, Ui);

            Assert.True(h.Engine.Snapshot().StudioMode);
            Assert.Null(h.Engine.Snapshot().PreviewSceneUuid);
            Assert.Equal(ReadKind.Preview, TheRead(h).Kind);
            Assert.Equal(h.World.Program, h.Engine.Snapshot().PreviewSceneUuid);
            var change = Assert.Single(Changes(h, ModelChangeKind.PreviewSceneChanged));
            Assert.Null(change.OldValue);
            Assert.Equal(h.World.Program, change.NewValue);
        }

        [Fact]
        public void SwitchingStudioModeOff_TakesThePreviewSceneAway_WithoutARead()
        {
            var h = Started(world => world.StudioMode = true);
            Assert.NotNull(h.Engine.Snapshot().PreviewSceneUuid);
            h.World.StudioMode = false;

            h.Event("StudioModeStateChanged", new { studioModeEnabled = false }, Ui);

            var snapshot = h.Engine.Snapshot();
            Assert.False(snapshot.StudioMode);
            Assert.Null(snapshot.PreviewSceneUuid);
            Assert.Null(h.Take());
            var changes = h.Engine.TakeChanges();
            var change = Assert.Single(changes, c => c.Kind == ModelChangeKind.PreviewSceneChanged);
            Assert.Null(change.NewValue);
            Assert.Single(changes, c => c.Kind == ModelChangeKind.StudioModeChanged);
        }

        [Fact]
        public void TheEventAboutThePreviewScene_IsFollowedByARead_AndTheModelHoldsWhatTheReadSays()
        {
            Guid gaming = default;
            var h = Started(world =>
            {
                gaming = world.AddScene("Gaming");
                world.StudioMode = true;
            });
            h.World.Preview = gaming;

            h.Event("CurrentPreviewSceneChanged", new { sceneName = "Gaming", sceneUuid = gaming }, Scenes);

            Assert.NotEqual(gaming, h.Engine.Snapshot().PreviewSceneUuid);
            Assert.Equal(ReadKind.Preview, TheRead(h).Kind);
            Assert.Equal(gaming, h.Engine.Snapshot().PreviewSceneUuid);
            var change = Assert.Single(Changes(h, ModelChangeKind.PreviewSceneChanged));
            Assert.Equal((gaming, "Gaming"), (change.Subject, change.Text));
        }

        [Fact]
        public void APreviewSceneThatOBSStudioRefuses_MeansNone_AndStudioModeIsReadAgainWhenTheModelThoughtItWasOn()
        {
            var h = Started(world => world.StudioMode = true);
            h.World.StudioMode = false;
            h.Event("CurrentPreviewSceneChanged", new { sceneName = "Scene", sceneUuid = h.World.Program }, Scenes);

            Assert.Equal(ReadKind.Preview, TheRead(h).Kind);

            Assert.Null(h.Engine.Snapshot().PreviewSceneUuid);
            Assert.Equal(ReadKind.StudioMode, TheRead(h).Kind);
            Assert.False(h.Engine.Snapshot().StudioMode);
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void APreviewSceneThatOBSStudioRefusesForGood_IsKnownToBeNone_WhenItRefusesThreeTimesInARow()
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            h.World.StudioMode = true;
            h.Start();
            var asked = new List<ReadRequest>();

            // A server that does not know the request (a simulation, a test double) refuses it with 204, which is not a moment that passes.
            h.SettleWithTime(request =>
            {
                asked.Add(request);
                return request.Kind == ReadKind.Preview ? (204, null) : h.World.Answer(request);
            });

            Assert.Equal(3, asked.Count(request => request.Kind == ReadKind.Preview));
            var snapshot = h.Engine.Snapshot();
            Assert.True(snapshot.StudioMode);
            Assert.Null(snapshot.PreviewSceneUuid);
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void ARefusalToSayThePreviewScene_ThatDoesNotRepeat_IsAskedAgain_AndTheAnswerThatFollowsIsHeld()
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            var gaming = h.World.AddScene("Gaming");
            h.World.StudioMode = true;
            h.World.Preview = gaming;
            h.Start();
            var refusals = 0;

            h.SettleWithTime(request =>
            {
                if (request.Kind == ReadKind.Preview && refusals < 2)
                {
                    refusals++;
                    return (204, null);
                }

                return h.World.Answer(request);
            });

            Assert.Equal(2, refusals);
            Assert.Equal(gaming, h.Engine.Snapshot().PreviewSceneUuid);
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void ARemovedPreviewScene_IsReadAgain_AndTheModelHoldsTheSceneThatOBSStudioPutsInItsPlace()
        {
            Guid gaming = default;
            Guid chat = default;
            var h = Started(world =>
            {
                chat = world.AddScene("Chat");
                gaming = world.AddScene("Gaming");
                world.StudioMode = true;
                world.Preview = gaming;
            });
            Assert.Equal(gaming, h.Engine.Snapshot().PreviewSceneUuid);

            // What OBS Studio does when the preview scene is removed: another scene becomes the preview scene (the one that is first in the list, which is not the program scene here).
            h.World.Scenes.RemoveAll(scene => scene.Uuid == gaming);
            h.World.Preview = h.World.Scenes[0].Uuid;
            h.Event("SceneRemoved", new { sceneName = "Gaming", sceneUuid = gaming, isGroup = false }, Scenes);
            var asked = Settle(h);

            Assert.Contains(asked, request => request.Kind == ReadKind.Preview);
            Assert.Equal(chat, h.Engine.Snapshot().PreviewSceneUuid);
            Assert.NotEqual(h.World.Program, h.Engine.Snapshot().PreviewSceneUuid);
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void ANewConnection_ReadsThePreviewSceneAgain_WhichChangedWhileTheModelWasAway()
        {
            Guid gaming = default;
            var h = Started(world =>
            {
                gaming = world.AddScene("Gaming");
                world.StudioMode = true;
            });
            Assert.Equal(h.World.Program, h.Engine.Snapshot().PreviewSceneUuid);

            h.End();
            h.World.Preview = gaming;
            h.Start();
            h.SettleWithTime();

            Assert.Equal(gaming, h.Engine.Snapshot().PreviewSceneUuid);
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void ANewConnection_FindsStudioModeOff_WhenItWasSwitchedOffWhileTheModelWasAway()
        {
            var h = Started(world => world.StudioMode = true);

            h.End();
            h.World.StudioMode = false;
            h.Start();
            h.SettleWithTime();

            var snapshot = h.Engine.Snapshot();
            Assert.False(snapshot.StudioMode);
            Assert.Null(snapshot.PreviewSceneUuid);
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void ATransition_IsRunningFromItsStartToTheEndOfItsVideo_AndAtItsStartTheProgramSceneIsReadAtOnce()
        {
            Guid gaming = default;
            var h = Started(world => gaming = world.AddScene("Gaming"));
            h.World.Program = gaming;
            var fade = h.Engine.Snapshot().Transition!;

            Started(h, fade.Name, fade.Uuid);

            // OBS Studio answers with the scene that it goes to at once, and announces it when the transition has ended: it is read now.
            Assert.True(h.Engine.Snapshot().TransitionRunning);
            Assert.Equal(ReadKind.Program, TheRead(h).Kind);
            Assert.Equal(gaming, h.Engine.Snapshot().ProgramSceneUuid);

            VideoEnded(h, fade.Name, fade.Uuid);

            Assert.False(h.Engine.Snapshot().TransitionRunning);
            var running = h.Engine.TakeChanges().Where(change => change.Kind == ModelChangeKind.TransitionRunningChanged).Select(change => ((bool)change.OldValue!, (bool)change.NewValue!)).ToArray();
            Assert.Equal([(false, true), (true, false)], running);
        }

        [Fact]
        public void ATransitionThatEndsWithoutItsVideoEnding_IsNotRunningAnymore()
        {
            var h = Started();
            var fade = h.Engine.Snapshot().Transition!;
            Started(h, fade.Name, fade.Uuid);
            Assert.True(h.Engine.Snapshot().TransitionRunning);

            // A transition that the next one cancels says that it ended, and does not say that its video did.
            Ended(h, fade.Name, fade.Uuid);

            Assert.False(h.Engine.Snapshot().TransitionRunning);
        }

        [Fact]
        public void AStinger_GoesOnRunningWhenItReachesItsCutPoint_UntilItsVideoEnds()
        {
            var stinger = Guid.NewGuid();
            var h = Started(world => world.Transition = ("Stinger", stinger, StingerKind, false, true));
            Started(h, "Stinger", stinger);

            Ended(h, "Stinger", stinger);

            Assert.True(h.Engine.Snapshot().TransitionRunning);

            VideoEnded(h, "Stinger", stinger);

            Assert.False(h.Engine.Snapshot().TransitionRunning);
        }

        [Fact]
        public void TheEndOfATransitionThatIsNotTheCurrentOne_EndsTheRunningOne_EvenWhenTheCurrentOneIsAStinger()
        {
            var stinger = Guid.NewGuid();
            var h = Started(world => world.Transition = ("Stinger", stinger, StingerKind, false, true));
            Started(h, "Fade", Guid.NewGuid());

            Ended(h, "Fade", Guid.NewGuid());

            Assert.False(h.Engine.Snapshot().TransitionRunning);
        }

        [Fact]
        public void AConnectionThatEndsDuringATransition_LeavesNoRunningTransitionBehind()
        {
            var h = Started();
            var fade = h.Engine.Snapshot().Transition!;
            Started(h, fade.Name, fade.Uuid);
            Assert.True(h.Engine.Snapshot().TransitionRunning);

            h.End();
            h.Start();
            h.SettleWithTime();

            Assert.False(h.Engine.Snapshot().TransitionRunning);
        }

        [Fact]
        public void ANewSceneCollection_IsReadAsAFirstOneIs_WithItsStudioModeAndItsTransition()
        {
            var h = Started(world =>
            {
                world.AddScene("Gaming");
                world.StudioMode = true;
            });
            Assert.True(h.Engine.Snapshot().StudioMode);
            Assert.NotNull(h.Engine.Snapshot().PreviewSceneUuid);

            h.Event("CurrentSceneCollectionChanging", new { sceneCollectionName = "Untitled" }, 2);
            h.World.Scenes.Clear();
            h.World.Items.Clear();
            h.World.Program = null;
            h.World.AddScene("Chatting");
            h.World.StudioMode = false;
            h.World.Transition = ("Cut", Guid.NewGuid(), "cut_transition", true, false);
            h.Event("CurrentSceneCollectionChanged", new { sceneCollectionName = "Chatting" }, 2);
            h.SettleWithTime();

            var snapshot = h.Engine.Snapshot();
            Assert.False(snapshot.StudioMode);
            Assert.Null(snapshot.PreviewSceneUuid);
            Assert.Equal("Cut", snapshot.Transition!.Name);
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void AReadThatOBSStudioDoesNotAnswerYet_IsAskedAgain()
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            h.Start();
            var notReady = 0;

            h.SettleWithTime(request =>
            {
                if (request.Kind == ReadKind.Transition && notReady < 2)
                {
                    notReady++;
                    return (207, null);
                }

                return h.World.Answer(request);
            });

            Assert.Equal(2, notReady);
            Assert.Equal("Fade", h.Engine.Snapshot().Transition!.Name);
            Assert.True(h.Engine.IsSettled);
        }
    }
}
