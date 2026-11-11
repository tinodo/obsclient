namespace OBSStudioClient.Tests.Model
{
    using System.Diagnostics;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Events;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Model;
    using OBSStudioClient.Tests.TestSupport;

    /// <summary>
    /// Studio mode, the preview scene and the scene transitions: what the model shows, what the calls send, and how they end. OBS Studio is made to do what it was measured to do with OBS Studio
    /// 33.0.0 (see LiveTransitionTests): the program scene is the new one at once for everything that asks, and the events come when the transition has ended.
    /// </summary>
    public class ModelTransitionTests
    {
        private const int Scenes = 4;
        private const int Transitions = 16;
        private const int Ui = 1024;
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private static Task<CallFixture> StartAsync(Action<FakeWorld>? setup = null, ObsModelOptions? options = null)
        {
            return CallFixture.StartAsync(options, world =>
            {
                world.AddScene("Other");
                setup?.Invoke(world);
            });
        }

        private static ObsModelOptions LongConfirmation()
        {
            return new ObsModelOptions { ConfirmationTimeout = TimeSpan.FromSeconds(5), WriteTimeout = TimeSpan.FromSeconds(10) };
        }

        private static Guid UuidOf(FakeWorld world, string name)
        {
            return world.Scenes.First(scene => scene.Name == name).Uuid;
        }

        private static async Task SendAsync(CallFixture test, string type, object data, int intent)
        {
            await test.Rig.Responder.SendEventAsync(type, data, intent);
        }

        private static Task TransitionEventAsync(CallFixture test, string type, ObsTransition transition)
        {
            return SendAsync(test, type, new { transitionName = transition.Name, transitionUuid = transition.Uuid }, Transitions);
        }

        // What OBS Studio does when studio mode is switched: the world holds it, the preview scene is the program scene, and the event follows the answer.
        private static void ActLikeObsStudio(CallFixture test)
        {
            test.Answer("SetStudioModeEnabled", data =>
            {
                var enabled = data!.Value.GetProperty("studioModeEnabled").GetBoolean();
                test.Rig.Responder.Change(world =>
                {
                    world.StudioMode = enabled;
                    world.Preview = null;
                });
                _ = Task.Run(() => SendAsync(test, "StudioModeStateChanged", new { studioModeEnabled = enabled }, Ui));
                return (100, null);
            });
        }

        private static List<string> Watch(ObsTransitions transitions)
        {
            var raised = new List<string>();
            transitions.PropertyChanged += (_, e) =>
            {
                lock (raised)
                {
                    raised.Add(e.PropertyName!);
                }
            };
            return raised;
        }

        private static string[] Told(List<string> list)
        {
            lock (list)
            {
                return list.ToArray();
            }
        }

        [Fact]
        public async Task TheModel_ShowsStudioMode_ThePreviewScene_AndTheCurrentTransition_AsOBSStudioHoldsThem()
        {
            await using var test = await StartAsync(world =>
            {
                world.StudioMode = true;
                world.Preview = UuidOf(world, "Other");
            });

            Assert.True(test.Model.IsStudioModeEnabled);
            Assert.Same(test.Model.Scenes["Other"], test.Model.PreviewScene);
            Assert.True(test.Model.Scenes["Other"].IsPreview);
            Assert.False(test.Gaming.IsPreview);
            Assert.True(test.Model.Scenes["Scene"].IsProgram);
            var transitions = test.Model.Transitions;
            Assert.Equal(("Fade", "fade_transition", false, false), (transitions.Current!.Name, transitions.Current.Kind, transitions.Current.IsFixed, transitions.Current.IsConfigurable));
            Assert.Equal(TimeSpan.FromMilliseconds(300), transitions.Duration);
            Assert.False(transitions.IsRunning);
            Assert.Contains("Fade", transitions.Current.ToString());
        }

        [Fact]
        public async Task WithStudioModeOff_ThereIsNoPreviewScene()
        {
            await using var test = await StartAsync();

            Assert.False(test.Model.IsStudioModeEnabled);
            Assert.Null(test.Model.PreviewScene);
            Assert.False(test.Gaming.IsPreview);
            Assert.Equal(0, test.Count("GetCurrentPreviewScene"));
        }

        [Fact]
        public async Task SwitchingStudioModeOn_ReturnsWhenTheModelShowsIt_AndThePreviewSceneFollows()
        {
            await using var test = await StartAsync();
            ActLikeObsStudio(test);

            await test.Model.SetStudioModeEnabledAsync(true).WithTimeoutAsync(Timeout);

            Assert.True(test.Model.IsStudioModeEnabled);
            Assert.True(test.AllSent("SetStudioModeEnabled").Single().GetProperty("studioModeEnabled").GetBoolean());
            await ModelRig.EventuallyAsync(() => test.Model.PreviewScene is not null, "the preview scene to be read");

            // OBS Studio makes the program scene the preview scene when it switches studio mode on, and sends no event for it.
            var program = test.Model.Scenes["Scene"];
            Assert.Same(program, test.Model.PreviewScene);
            Assert.True(program.IsPreview);
            await ModelRig.EventuallyAsync(() => test.Rig.Changes.Any(change => change.Entity is null && change.PropertyName == nameof(ObsModel.PreviewScene)), "the record of the preview scene");
        }

        [Fact]
        public async Task SwitchingStudioModeOff_ReturnsWhenTheModelShowsIt_AndThePreviewSceneIsGone()
        {
            await using var test = await StartAsync(world => world.StudioMode = true);
            ActLikeObsStudio(test);
            Assert.NotNull(test.Model.PreviewScene);

            await test.Model.SetStudioModeEnabledAsync(false).WithTimeoutAsync(Timeout);

            Assert.False(test.Model.IsStudioModeEnabled);
            Assert.Null(test.Model.PreviewScene);
            Assert.False(test.Gaming.IsPreview);
        }

        [Fact]
        public async Task ASceneIsMadeThePreviewScene_ByNameOfItsUuid_AndTheModelSaysSoWhenTheEventComes()
        {
            await using var test = await StartAsync(world => world.StudioMode = true);
            var other = test.Model.Scenes["Other"];
            var raised = new List<string>();
            other.PropertyChanged += (_, e) =>
            {
                lock (raised)
                {
                    raised.Add(e.PropertyName!);
                }
            };
            test.Answer("SetCurrentPreviewScene", request =>
            {
                test.Rig.Responder.Change(world => world.Preview = other.Uuid);
                _ = Task.Run(() => SendAsync(test, "CurrentPreviewSceneChanged", new { sceneName = "Other", sceneUuid = other.Uuid }, Scenes));
                return (100, null);
            });

            await other.SwitchToPreviewAsync().WithTimeoutAsync(Timeout);

            Assert.True(other.IsPreview);
            Assert.Same(other, test.Model.PreviewScene);
            Assert.False(test.Gaming.IsPreview);
            Assert.Equal(other.Uuid, test.AllSent("SetCurrentPreviewScene").Single().GetProperty("sceneUuid").GetGuid());
            await ModelRig.EventuallyAsync(() => Told(raised).Contains(nameof(ObsScene.IsPreview)), "the notice of the scene");
        }

        [Fact]
        public async Task APreviewScene_WhileStudioModeIsOff_IsRefused_WithTheCodeThatSaysSo()
        {
            await using var test = await StartAsync();
            test.Answer("SetCurrentPreviewScene", 506);

            var refusal = await Assert.ThrowsAsync<ObsResponseException>(() => test.Model.Scenes["Other"].SwitchToPreviewAsync().WithTimeoutAsync(Timeout));

            Assert.Equal(RequestStatusCode.StudioModeNotActive, refusal.ErrorCode);
        }

        [Fact]
        public async Task ARemovedScene_CanNotBeThePreviewScene()
        {
            await using var test = await StartAsync(world => world.StudioMode = true);
            var other = test.Model.Scenes["Other"];
            await SendAsync(test, "SceneRemoved", new { sceneName = "Other", sceneUuid = other.Uuid, isGroup = false }, Scenes);
            await ModelRig.EventuallyAsync(() => other.IsRemoved, "the scene to be removed");

            await Assert.ThrowsAsync<ObsRemovedException>(() => other.SwitchToPreviewAsync().WithTimeoutAsync(Timeout));

            Assert.Equal(0, test.Count("SetCurrentPreviewScene"));
        }

        [Fact]
        public async Task ATransitionIsMadeTheCurrentOne_ByName_AndTheCallReturnsWhenTheModelShowsIt()
        {
            await using var test = await StartAsync();
            var transitions = test.Model.Transitions;
            var cut = Guid.NewGuid();
            var raised = Watch(transitions);
            test.Answer("SetCurrentSceneTransition", request =>
            {
                test.Rig.Responder.Change(world => world.Transition = ("Cut", cut, "cut_transition", true, false));
                _ = Task.Run(() => SendAsync(test, "CurrentSceneTransitionChanged", new { transitionName = "Cut", transitionUuid = cut }, Transitions));
                return (100, null);
            });

            await transitions.SetCurrentAsync("Cut").WithTimeoutAsync(Timeout);

            Assert.Equal(("Cut", cut, "cut_transition", true), (transitions.Current!.Name, transitions.Current.Uuid, transitions.Current.Kind, transitions.Current.IsFixed));
            Assert.Equal("Cut", test.AllSent("SetCurrentSceneTransition").Single().GetProperty("transitionName").GetString());
            await ModelRig.EventuallyAsync(() => Told(raised).Contains(nameof(ObsTransitions.Current)), "the notice");
            var record = Assert.Single(test.Rig.Changes, change => ReferenceEquals(change.Entity, transitions) && change.PropertyName == nameof(ObsTransitions.Current));
            Assert.Equal("Fade", ((ObsTransition)record.OldValue!).Name);
            Assert.Equal("Cut", ((ObsTransition)record.NewValue!).Name);
            Assert.Contains("scene transitions", record.ToString());
        }

        [Fact]
        public async Task ATransitionThatOBSStudioDoesNotHave_IsAnObsResponseException_NotARemoval()
        {
            await using var test = await StartAsync();
            test.Answer("SetCurrentSceneTransition", 600, "No scene transition was found by that name.");

            var refusal = await Assert.ThrowsAsync<ObsResponseException>(() => test.Model.Transitions.SetCurrentAsync("Spin").WithTimeoutAsync(Timeout));

            Assert.Equal(RequestStatusCode.ResourceNotFound, refusal.ErrorCode);
            Assert.Contains("No scene transition", refusal.Message);
            Assert.Equal("Fade", test.Model.Transitions.Current!.Name);
        }

        [Fact]
        public async Task ATransitionWithoutAName_IsAnArgumentException_AndNothingIsSent()
        {
            await using var test = await StartAsync();

            Assert.Throws<ArgumentException>(() => { _ = test.Model.Transitions.SetCurrentAsync(string.Empty); });
            Assert.Throws<ArgumentNullException>(() => { _ = test.Model.Transitions.SetCurrentAsync(null!); });

            Assert.Equal(0, test.Count("SetCurrentSceneTransition"));
        }

        [Fact]
        public async Task TheDuration_IsSetInWholeMilliseconds_AndTheCallReturnsWhenTheModelShowsIt()
        {
            await using var test = await StartAsync();
            var transitions = test.Model.Transitions;
            var raised = Watch(transitions);
            test.Answer("SetCurrentSceneTransitionDuration", data =>
            {
                var milliseconds = data!.Value.GetProperty("transitionDuration").GetInt32();
                test.Rig.Responder.Change(world => world.TransitionDuration = milliseconds);
                _ = Task.Run(() => SendAsync(test, "CurrentSceneTransitionDurationChanged", new { transitionDuration = milliseconds }, Transitions));
                return (100, null);
            });

            await transitions.SetDurationAsync(TimeSpan.FromMilliseconds(1234.6)).WithTimeoutAsync(Timeout);

            Assert.Equal(TimeSpan.FromMilliseconds(1235), transitions.Duration);
            Assert.Equal(1235, test.AllSent("SetCurrentSceneTransitionDuration").Single().GetProperty("transitionDuration").GetInt32());
            await ModelRig.EventuallyAsync(() => Told(raised).Contains(nameof(ObsTransitions.Duration)), "the notice");
            var record = Assert.Single(test.Rig.Changes, change => ReferenceEquals(change.Entity, transitions) && change.PropertyName == nameof(ObsTransitions.Duration));
            Assert.Equal((TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(1235)), ((TimeSpan)record.OldValue!, (TimeSpan)record.NewValue!));
        }

        [Fact]
        public async Task ADurationThatIsTheDurationAlready_ReturnsAtOnce_BecauseOBSStudioSaysNothingThen()
        {
            await using var test = await StartAsync();
            test.Answer("SetCurrentSceneTransitionDuration");

            await test.Model.Transitions.SetDurationAsync(TimeSpan.FromMilliseconds(300)).WithTimeoutAsync(Timeout);

            Assert.Equal(TimeSpan.FromMilliseconds(300), test.Model.Transitions.Duration);
        }

        [Theory]
        [InlineData(49)]
        [InlineData(20001)]
        [InlineData(-1)]
        public async Task ADurationOutOfRange_IsAnArgumentOutOfRangeException_AndNothingIsSent(double milliseconds)
        {
            await using var test = await StartAsync();

            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = test.Model.Transitions.SetDurationAsync(TimeSpan.FromMilliseconds(milliseconds)); });
            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = test.Model.Scenes["Other"].SetTransitionOverrideAsync("Fade", TimeSpan.FromMilliseconds(milliseconds)); });

            Assert.Equal(0, test.Count("SetCurrentSceneTransitionDuration") + test.Count("SetSceneSceneTransitionOverride"));
        }

        [Fact]
        public async Task WhileACutIsTheCurrentTransition_TheDurationIsUnknown_UntilItIsSaid()
        {
            await using var test = await StartAsync(world => world.Transition = ("Cut", Guid.NewGuid(), "cut_transition", true, false));
            var transitions = test.Model.Transitions;
            Assert.True(transitions.Current!.IsFixed);
            Assert.Null(transitions.Duration);

            await SendAsync(test, "CurrentSceneTransitionDurationChanged", new { transitionDuration = 700 }, Transitions);

            await ModelRig.EventuallyAsync(() => transitions.Duration == TimeSpan.FromMilliseconds(700), "the duration");
        }

        [Fact]
        public async Task TheListOfTransitionsAndTheirKinds_AreAskedFor_AndNotKept()
        {
            await using var test = await StartAsync();
            var swipe = Guid.NewGuid();
            test.Answer("GetSceneTransitionList", 100, new
            {
                currentSceneTransitionKind = "fade_transition",
                currentSceneTransitionName = "Fade",
                currentSceneTransitionUuid = test.Model.Transitions.Current!.Uuid,
                transitions = new object[]
                {
                    new { transitionConfigurable = false, transitionFixed = false, transitionKind = "fade_transition", transitionName = "Fade", transitionUuid = test.Model.Transitions.Current.Uuid },
                    new { transitionConfigurable = true, transitionFixed = false, transitionKind = "swipe_transition", transitionName = "Swipe", transitionUuid = swipe },
                },
            });
            test.Answer("GetTransitionKindList", 100, new { transitionKinds = new[] { "cut_transition", "fade_transition", "swipe_transition" } });

            var all = await test.Model.Transitions.GetAllAsync().WithTimeoutAsync(Timeout);
            var kinds = await test.Model.Transitions.GetKindsAsync().WithTimeoutAsync(Timeout);

            Assert.Equal(new[] { "Fade", "Swipe" }, all.Select(transition => transition.Name).ToArray());
            Assert.Equal((swipe, "swipe_transition", false, true), (all[1].Uuid, all[1].Kind, all[1].IsFixed, all[1].IsConfigurable));
            Assert.Equal(new[] { "cut_transition", "fade_transition", "swipe_transition" }, kinds.ToArray());
        }

        [Fact]
        public async Task TheSettingsOfTheCurrentTransition_AreAskedFor_AndAreEmptyForOneThatHasNone()
        {
            await using var test = await StartAsync();

            var none = await test.Model.Transitions.GetSettingsAsync().WithTimeoutAsync(Timeout);
            Assert.Equal(System.Text.Json.JsonValueKind.Object, none.ValueKind);
            Assert.Empty(none.EnumerateObject());

            var uuid = Guid.NewGuid();
            test.Answer("GetCurrentSceneTransition", 100, new { transitionConfigurable = true, transitionDuration = 300, transitionFixed = false, transitionKind = "swipe_transition", transitionName = "Swipe", transitionSettings = new { direction = "left" }, transitionUuid = uuid });
            var some = await test.Model.Transitions.GetSettingsAsync().WithTimeoutAsync(Timeout);
            Assert.Equal("left", some.GetProperty("direction").GetString());
        }

        [Fact]
        public async Task TheSettingsOfTheCurrentTransition_AreChanged_WithOrWithoutOverlay_AndARefusalSaysThatItHasNone()
        {
            await using var test = await StartAsync();
            test.Answer("SetCurrentSceneTransitionSettings");

            await test.Model.Transitions.SetSettingsAsync(new { direction = "right" }).WithTimeoutAsync(Timeout);
            await test.Model.Transitions.SetSettingsAsync(new { direction = "up" }, overlay: false).WithTimeoutAsync(Timeout);

            var sent = test.AllSent("SetCurrentSceneTransitionSettings");
            Assert.Equal(("right", true), (sent[0].GetProperty("transitionSettings").GetProperty("direction").GetString(), sent[0].GetProperty("overlay").GetBoolean()));
            Assert.Equal(("up", false), (sent[1].GetProperty("transitionSettings").GetProperty("direction").GetString(), sent[1].GetProperty("overlay").GetBoolean()));

            test.Answer("SetCurrentSceneTransitionSettings", 606, "The current transition does not support custom settings.");
            var refusal = await Assert.ThrowsAsync<ObsResponseException>(() => test.Model.Transitions.SetSettingsAsync(new { direction = "left" }).WithTimeoutAsync(Timeout));
            Assert.Equal(RequestStatusCode.ResourceNotConfigurable, refusal.ErrorCode);
            Assert.Throws<ArgumentException>(() => { _ = test.Model.Transitions.SetSettingsAsync(42); });
        }

        [Fact]
        public async Task TheProgress_IsAskedFor_AndIsAPositionFromZeroToOne()
        {
            await using var test = await StartAsync();
            test.Answer("GetCurrentSceneTransitionCursor", 100, new { transitionCursor = 0.25 });

            var progress = await test.Model.Transitions.GetProgressAsync().WithTimeoutAsync(Timeout);

            Assert.Equal(0.25, progress);
        }

        [Fact]
        public async Task TheTransitionOfStudioMode_IsTriggered_AndTheCallReturnsWhenItHasStarted_NotWhenItHasEnded()
        {
            await using var test = await StartAsync(world =>
            {
                world.StudioMode = true;
                world.Preview = UuidOf(world, "Other");
            });
            var transitions = test.Model.Transitions;
            var other = test.Model.Scenes["Other"];
            var before = test.Model.Scenes["Scene"];
            var fade = transitions.Current!;
            var heard = new List<string>();
            transitions.Started += (_, e) => Hear(heard, "Started", e);
            transitions.Ended += (_, e) => Hear(heard, "Ended", e);
            transitions.VideoEnded += (_, e) => Hear(heard, "VideoEnded", e);
            test.Answer("TriggerStudioModeTransition", request =>
            {
                // OBS Studio puts the preview scene on the air at once as far as a question goes, and announces it when the transition has ended.
                test.Rig.Responder.Change(world => world.Program = other.Uuid);
                _ = Task.Run(() => TransitionEventAsync(test, "SceneTransitionStarted", fade));
                return (100, null);
            });

            await transitions.TriggerAsync().WithTimeoutAsync(Timeout);

            Assert.True(transitions.IsRunning);
            await ModelRig.EventuallyAsync(() => ReferenceEquals(test.Model.ProgramScene, other), "the scene that is switched to to be read at the start of the transition");
            Assert.True(other.IsProgram);
            Assert.False(before.IsProgram);

            // The end: the video ends, the preview scene is the scene that was on the air, and the program scene is announced.
            test.Rig.Responder.Change(world => world.Preview = before.Uuid);
            await TransitionEventAsync(test, "SceneTransitionVideoEnded", fade);
            await SendAsync(test, "CurrentPreviewSceneChanged", new { sceneName = "Scene", sceneUuid = before.Uuid }, Scenes);
            await SendAsync(test, "CurrentProgramSceneChanged", new { sceneName = "Other", sceneUuid = other.Uuid }, Scenes);
            await TransitionEventAsync(test, "SceneTransitionEnded", fade);
            await ModelRig.EventuallyAsync(() => !transitions.IsRunning && before.IsPreview && Told(heard).Length == 3, "the end of the transition");

            Assert.Equal(new[] { "Started Fade", "VideoEnded Fade", "Ended Fade" }, Told(heard));
            Assert.Same(other, test.Model.ProgramScene);
            Assert.Same(before, test.Model.PreviewScene);
        }

        private static void Hear(List<string> heard, string what, TransitionNameEventArgs e)
        {
            lock (heard)
            {
                heard.Add($"{what} {e.TransitionName}");
            }
        }

        [Fact]
        public async Task ATriggerWhileStudioModeIsOff_IsRefused_WithTheCodeThatSaysSo()
        {
            await using var test = await StartAsync();
            test.Answer("TriggerStudioModeTransition", 506);

            var refusal = await Assert.ThrowsAsync<ObsResponseException>(() => test.Model.Transitions.TriggerAsync().WithTimeoutAsync(Timeout));

            Assert.Equal(RequestStatusCode.StudioModeNotActive, refusal.ErrorCode);
        }

        [Fact]
        public async Task ATriggerThatNoEventFollows_EndsWhenTheProgramSceneHasBeenReadAgain()
        {
            await using var test = await StartAsync(world => world.StudioMode = true);
            var other = test.Model.Scenes["Other"];
            test.Answer("TriggerStudioModeTransition", request =>
            {
                test.Rig.Responder.Change(world => world.Program = other.Uuid);
                return (100, null);
            });
            var reads = test.Count("GetCurrentProgramScene");

            await test.Model.Transitions.TriggerAsync().WithTimeoutAsync(Timeout);

            // No event said that a transition started: the model was read again, and the scene that OBS Studio put on the air is the one that it shows.
            Assert.True(test.Count("GetCurrentProgramScene") > reads);
            Assert.Same(other, test.Model.ProgramScene);
            Assert.False(test.Model.Transitions.IsRunning);
        }

        [Fact]
        public async Task ASwitchToAScene_ReturnsWhenTheTransitionHasStarted_AndNotWhenItHasEnded()
        {
            await using var test = await StartAsync(options: LongConfirmation());
            var other = test.Model.Scenes["Other"];
            var fade = test.Model.Transitions.Current!;
            test.Answer("SetCurrentProgramScene", request =>
            {
                test.Rig.Responder.Change(world => world.Program = other.Uuid);
                _ = Task.Run(() => TransitionEventAsync(test, "SceneTransitionStarted", fade));
                return (100, null);
            });

            // The event that says that the scene is the program scene comes when the transition has ended: the model reads it when the transition starts, and the call does not wait for the end.
            var stopwatch = Stopwatch.StartNew();
            await other.SwitchToAsync().WithTimeoutAsync(TimeSpan.FromSeconds(3));

            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(3));
            Assert.True(other.IsProgram);
            Assert.True(test.Model.Transitions.IsRunning);
        }

        [Fact]
        public async Task TheTBar_IsSetToAPosition_AndOnlyToOneFromZeroToOne()
        {
            await using var test = await StartAsync(world => world.StudioMode = true);
            test.Answer("SetTBarPosition");

            await test.Model.Transitions.SetTBarPositionAsync(0.5, release: false).WithTimeoutAsync(Timeout);
            await test.Model.Transitions.SetTBarPositionAsync(1.0).WithTimeoutAsync(Timeout);

            var sent = test.AllSent("SetTBarPosition");
            Assert.Equal((0.5, false), (sent[0].GetProperty("position").GetDouble(), sent[0].GetProperty("release").GetBoolean()));
            Assert.Equal((1.0, true), (sent[1].GetProperty("position").GetDouble(), sent[1].GetProperty("release").GetBoolean()));
            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = test.Model.Transitions.SetTBarPositionAsync(-0.1); });
            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = test.Model.Transitions.SetTBarPositionAsync(1.1); });
            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = test.Model.Transitions.SetTBarPositionAsync(double.NaN); });
            Assert.Equal(2, test.Count("SetTBarPosition"));
        }

        [Fact]
        public async Task TheTransitionOfAScene_IsAskedFor_WhetherItHasOneOrNot()
        {
            await using var test = await StartAsync();
            var other = test.Model.Scenes["Other"];
            test.Answer("GetSceneSceneTransitionOverride", 100, new { transitionName = "Fade", transitionDuration = 500 });
            var set = await other.GetTransitionOverrideAsync().WithTimeoutAsync(Timeout);

            test.Answer("GetSceneSceneTransitionOverride", 100, new { transitionName = (string?)null, transitionDuration = (int?)null });
            var none = await other.GetTransitionOverrideAsync().WithTimeoutAsync(Timeout);

            Assert.Equal((true, "Fade", TimeSpan.FromMilliseconds(500)), (set.IsSet, set.Name, set.Duration));
            Assert.Equal((false, (string?)null, (TimeSpan?)null), (none.IsSet, none.Name, none.Duration));
            Assert.Equal(other.Uuid, test.AllSent("GetSceneSceneTransitionOverride")[0].GetProperty("sceneUuid").GetGuid());
        }

        [Fact]
        public async Task TheTransitionOfAScene_IsSetAndTakenAway_AndNothingIsLeftOutOfWhatIsSent()
        {
            await using var test = await StartAsync();
            var other = test.Model.Scenes["Other"];
            test.Answer("SetSceneSceneTransitionOverride");

            await other.SetTransitionOverrideAsync("Fade", TimeSpan.FromSeconds(2)).WithTimeoutAsync(Timeout);
            await other.SetTransitionOverrideAsync(null, TimeSpan.FromSeconds(1)).WithTimeoutAsync(Timeout);
            await other.ClearTransitionOverrideAsync().WithTimeoutAsync(Timeout);

            var sent = test.AllSent("SetSceneSceneTransitionOverride");
            Assert.Equal(3, sent.Length);
            Assert.All(sent, data => Assert.Equal(other.Uuid, data.GetProperty("sceneUuid").GetGuid()));
            Assert.Equal(("Fade", 2000), (sent[0].GetProperty("transitionName").GetString(), sent[0].GetProperty("transitionDuration").GetInt32()));
            Assert.Equal((System.Text.Json.JsonValueKind.Null, 1000), (sent[1].GetProperty("transitionName").ValueKind, sent[1].GetProperty("transitionDuration").GetInt32()));

            // Nothing but nulls takes the override away: OBS Studio needs them to be there.
            Assert.Equal((System.Text.Json.JsonValueKind.Null, System.Text.Json.JsonValueKind.Null), (sent[2].GetProperty("transitionName").ValueKind, sent[2].GetProperty("transitionDuration").ValueKind));
        }

        [Fact]
        public async Task ATransitionForAScene_ThatOBSStudioDoesNotHave_IsAnObsResponseException()
        {
            await using var test = await StartAsync();
            test.Answer("SetSceneSceneTransitionOverride", 600, "No scene transition was found by that name.");

            var refusal = await Assert.ThrowsAsync<ObsResponseException>(() => test.Model.Scenes["Other"].SetTransitionOverrideAsync("Spin").WithTimeoutAsync(Timeout));

            Assert.Equal(RequestStatusCode.ResourceNotFound, refusal.ErrorCode);
        }

        [Fact]
        public async Task TheTransitionOfARemovedScene_CanNotBeAskedForOrSet()
        {
            await using var test = await StartAsync();
            var other = test.Model.Scenes["Other"];
            await SendAsync(test, "SceneRemoved", new { sceneName = "Other", sceneUuid = other.Uuid, isGroup = false }, Scenes);
            await ModelRig.EventuallyAsync(() => other.IsRemoved, "the scene to be removed");

            await Assert.ThrowsAsync<ObsRemovedException>(() => other.GetTransitionOverrideAsync().WithTimeoutAsync(Timeout));
            await Assert.ThrowsAsync<ObsRemovedException>(() => other.SetTransitionOverrideAsync("Fade").WithTimeoutAsync(Timeout));
        }

        [Fact]
        public async Task TheChangesOfTheTransitions_AreToldAsPropertyChanges_AndNothingIsToldTwice()
        {
            await using var test = await StartAsync();
            var transitions = test.Model.Transitions;
            var raised = Watch(transitions);
            var fade = transitions.Current!;

            await SendAsync(test, "CurrentSceneTransitionDurationChanged", new { transitionDuration = 1500 }, Transitions);
            await TransitionEventAsync(test, "SceneTransitionStarted", fade);
            await ModelRig.EventuallyAsync(() => Told(raised).Length >= 2, "the two notices");
            await TransitionEventAsync(test, "SceneTransitionVideoEnded", fade);
            await TransitionEventAsync(test, "SceneTransitionEnded", fade);
            await ModelRig.EventuallyAsync(() => Told(raised).Length == 3, "the three notices");

            Assert.Equal(new[] { nameof(ObsTransitions.Duration), nameof(ObsTransitions.IsRunning), nameof(ObsTransitions.IsRunning) }, Told(raised));
            Assert.Equal(TimeSpan.FromMilliseconds(1500), transitions.Duration);
        }
    }
}
