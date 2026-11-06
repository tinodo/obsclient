namespace OBSStudioClient.Tests.Studio
{
    using OBSStudioClient.Studio.Engine;

    /// <summary>
    /// What the engine remembers about what was removed, and for how long. A source that OBS destroyed is remembered until a list shows that OBS is done with
    /// it (OBS goes on listing an input that it keeps alive for something that nothing shows, and the model must not take it for a new input), and for a while
    /// after that: the messages that it guards against arrive within seconds. Without a limit the memory grows for as long as a connection lasts.
    /// </summary>
    public class StudioEngineRetentionTests
    {
        private static readonly StudioEngineOptions Quick = new() { TombstoneRetention = TimeSpan.FromMinutes(1), TombstoneCleanupInterval = TimeSpan.FromSeconds(10) };

        private static EngineHarness Started(Action<FakeWorld>? setup = null)
        {
            var harness = new EngineHarness(new StudioEngineOptions { TombstoneRetention = Quick.TombstoneRetention, TombstoneCleanupInterval = Quick.TombstoneCleanupInterval });
            harness.World.AddScene("Scene");
            setup?.Invoke(harness.World);
            harness.Start();
            harness.SettleWithTime();
            return harness;
        }

        // A message arrives after a time: that is when the engine looks at what it remembers.
        private static void LaterAMessageArrives(EngineHarness h, TimeSpan after, string scene, Func<ReadRequest, (int Code, object? Data)>? answer = null)
        {
            h.Advance(after);
            EngineEvents.SceneCreated(h, h.World.AddScene(scene), scene);
            h.SettleWithTime(answer);
        }

        [Fact]
        public void WhatWasDestroyed_IsRememberedForTheRetentionAfterAListConfirmedIt_AndThenLetGoOf()
        {
            Guid doomed = Guid.Empty;
            var h = Started(world =>
            {
                doomed = world.AddScene("Doomed");
                var mic = world.AddInput("Mic", "wasapi_input_capture", 2);
                world.AddItem(doomed, mic, "Mic");
            });
            Assert.Equal(0, h.Engine.RememberedCount);

            h.World.Scenes.RemoveAll(scene => scene.Uuid == doomed);
            h.World.Items.Remove(doomed);
            EngineEvents.SceneRemoved(h, doomed, "Doomed");
            h.SettleWithTime();
            var remembered = h.Engine.RememberedCount;

            // The scene and its scene item.
            Assert.Equal(2, remembered);

            // Within the retention nothing is let go of, whatever happens.
            LaterAMessageArrives(h, TimeSpan.FromSeconds(45), "One");
            Assert.Equal(remembered, h.Engine.RememberedCount);

            // After it, the next message lets go: the lists that were read since do not have the scene.
            LaterAMessageArrives(h, TimeSpan.FromMinutes(2), "Two");
            Assert.Equal(0, h.Engine.RememberedCount);
            Assert.Equal(remembered, h.Engine.LetGoCount);
        }

        [Fact]
        public void WhatWasRemovedBeforeTheModelKnewIt_IsLetGoOfToo()
        {
            var h = Started();
            EngineEvents.SceneRemoved(h, Guid.NewGuid(), "Unknown");
            EngineEvents.InputRemoved(h, Guid.NewGuid(), "Unknown");
            Assert.Equal(2, h.Engine.RememberedCount);

            // No list confirmed them: the engine reads the lists, which do not have them (it does so for what is old), and they are let go of after the retention.
            LaterAMessageArrives(h, TimeSpan.FromMinutes(2), "New");
            Assert.Equal(2, h.Engine.RememberedCount);
            LaterAMessageArrives(h, TimeSpan.FromMinutes(2), "Newer");

            Assert.Equal(0, h.Engine.RememberedCount);
        }

        [Fact]
        public void ASceneItemThatAReadFoundGone_IsLetGoOfWhenItsEventDoesNotCome()
        {
            Guid a = Guid.Empty;
            Guid mic = Guid.Empty;
            var h = Started(world =>
            {
                a = world.AddScene("A");
                mic = world.AddInput("Mic", "wasapi_input_capture", 2);
                world.AddItem(a, mic, "Mic");
            });
            h.World.Items[a].Clear();
            h.Event("SceneItemListReindexed", new { sceneName = "A", sceneUuid = a, sceneItems = new[] { new { sceneItemId = 99, sceneItemIndex = 0 } } }, 128);
            h.SettleWithTime();

            // The scene item and the hold on its source.
            Assert.Equal(2, h.Engine.RememberedCount);

            LaterAMessageArrives(h, TimeSpan.FromMinutes(2), "New");

            Assert.Equal(0, h.Engine.RememberedCount);
        }

        [Fact]
        public void TheSceneItemsThatWentWithASceneThatAReadFoundGone_AreLetGoOfWhenTheirSourceIsDestroyed()
        {
            Guid doomed = Guid.Empty;
            Guid mic = Guid.Empty;
            var h = Started(world =>
            {
                doomed = world.AddScene("Doomed");
                mic = world.AddInput("Mic", "wasapi_input_capture", 2);
                world.AddItem(doomed, mic, "Mic");
            });

            // OBS destroys Doomed, and the events are on their way: the model reads the list of scenes, which lacks it, and the scene items of Doomed cannot be read.
            // No event will tell about them, and the events about the input that they showed can still come: they are held.
            h.World.Scenes.RemoveAll(scene => scene.Uuid == doomed);
            h.World.Items.Remove(doomed);
            LaterAMessageArrives(h, TimeSpan.FromSeconds(1), "Other");
            Assert.DoesNotContain(h.Engine.Snapshot().Scenes, scene => scene.Uuid == doomed);
            Assert.Equal(1, h.Engine.HeldItemCount);

            // OBS destroys the input as well. What the events could still say about it is over, so what was held for it goes.
            h.World.Inputs.RemoveAll(input => input.Uuid == mic);
            EngineEvents.InputRemoved(h, mic, "Mic");
            h.SettleWithTime();

            Assert.DoesNotContain(h.Engine.Snapshot().Inputs, input => input.Uuid == mic);
            Assert.Equal(0, h.Engine.HeldItemCount);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ARemovedInputThatNothingUsesAnyMore_IsLetGoOf_WhenTheTimeIsUpForWhatWasHeldForIt(bool pinReleased)
        {
            Guid doomed = Guid.Empty;
            Guid mic = Guid.Empty;
            var h = Started(world =>
            {
                doomed = world.AddScene("Doomed");
                mic = world.AddInput("Mic", "wasapi_input_capture", 2);
                world.AddItem(doomed, mic, "Mic");
            });

            // OBS destroys Doomed and the events are on their way: the model reads the list of scenes, which lacks it, and holds what its scene item showed.
            h.World.Scenes.RemoveAll(scene => scene.Uuid == doomed);
            h.World.Items.Remove(doomed);
            LaterAMessageArrives(h, TimeSpan.FromSeconds(1), "Other");
            Assert.Equal(1, h.Engine.HeldItemCount);

            // OBS removes the input and keeps it alive, and the list says so (a global audio input that is assigned to a channel). With that held the model cannot
            // tell it from the input of a scene item that it does not know yet, so it keeps the input as one that was removed.
            EngineEvents.InputRemoved(h, mic, "Mic");
            h.SettleWithTime();
            Assert.Equal(SourceState.Detached, Assert.Single(h.Engine.Snapshot().Inputs).State);

            // OBS lets go of the pin, and destroys the input (it says nothing), or it does not.
            if (pinReleased)
            {
                h.World.Inputs.RemoveAll(input => input.Uuid == mic);
            }

            // What was held is let go of after the retention, and nothing uses the input: it goes, as it does when its last scene item goes.
            LaterAMessageArrives(h, TimeSpan.FromMinutes(2), "Two");

            Assert.Empty(h.Engine.Snapshot().Inputs);
            Assert.Equal(0, h.Engine.HeldItemCount);
            Assert.True(h.Engine.IsSettled);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void AnInputWhoseRemovalCheckIsStillPending_WhenTheTimeIsUpForWhatWasHeldForIt_IsLetGoOf_WhenTheCheckIsDone(bool pinReleased)
        {
            Guid doomed = Guid.Empty;
            Guid mic = Guid.Empty;
            var h = Started(world =>
            {
                doomed = world.AddScene("Doomed");
                mic = world.AddInput("Mic", "wasapi_input_capture", 2);
                world.AddItem(doomed, mic, "Mic");
            });
            h.World.Scenes.RemoveAll(scene => scene.Uuid == doomed);
            h.World.Items.Remove(doomed);
            LaterAMessageArrives(h, TimeSpan.FromSeconds(1), "Other");
            Assert.Equal(1, h.Engine.HeldItemCount);

            // OBS removes the input, and the model asks the list of inputs, which is not read yet when the time is up for what was held: the look at what the
            // engine remembers lets go of it while the check is pending, and the input is judged when the check is done.
            EngineEvents.InputRemoved(h, mic, "Mic");
            if (pinReleased)
            {
                h.World.Inputs.RemoveAll(input => input.Uuid == mic);
            }

            LaterAMessageArrives(h, TimeSpan.FromMinutes(2), "Two");

            Assert.Empty(h.Engine.Snapshot().Inputs);
            Assert.Equal(0, h.Engine.HeldItemCount);
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void AnInputThatIsStillHeldForWhenItsRemovalCheckIsDone_StaysWithoutAnotherCheck()
        {
            Guid doomed = Guid.Empty;
            Guid mic = Guid.Empty;
            var h = Started(world =>
            {
                doomed = world.AddScene("Doomed");
                mic = world.AddInput("Mic", "wasapi_input_capture", 2);
                world.AddItem(doomed, mic, "Mic");
            });
            h.World.Scenes.RemoveAll(scene => scene.Uuid == doomed);
            h.World.Items.Remove(doomed);
            LaterAMessageArrives(h, TimeSpan.FromSeconds(1), "Other");
            EngineEvents.InputRemoved(h, mic, "Mic");

            // The check is done while something is held: the input stays (the end of the hold judges it), and the list is not read for it again.
            var lists = 0;
            h.SettleWithTime(request =>
            {
                lists += request.Kind == ReadKind.Inputs ? 1 : 0;
                return h.World.Answer(request);
            });
            Assert.Equal(SourceState.Detached, Assert.Single(h.Engine.Snapshot().Inputs).State);
            Assert.Equal(1, lists);
            Assert.Null(h.Take());
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void AnInputThatIsStillHeldFor_IsLetGoOf_WhenTheEventOfTheLastSceneItemThatIsHeldForItArrives()
        {
            Guid doomed = Guid.Empty;
            Guid mic = Guid.Empty;
            var itemId = 0;
            var h = Started(world =>
            {
                doomed = world.AddScene("Doomed");
                mic = world.AddInput("Mic", "wasapi_input_capture", 2);
                itemId = world.AddItem(doomed, mic, "Mic");
            });
            h.World.Scenes.RemoveAll(scene => scene.Uuid == doomed);
            h.World.Items.Remove(doomed);
            LaterAMessageArrives(h, TimeSpan.FromSeconds(1), "Other");
            EngineEvents.InputRemoved(h, mic, "Mic");
            h.SettleWithTime();

            // The check is done and OBS still lists the input (a global audio input that is assigned to a channel), but a scene item that a read took away used it
            // when OBS removed it: the input stays, and it is judged when the hold ends.
            Assert.Equal(SourceState.Detached, Assert.Single(h.Engine.Snapshot().Inputs).State);
            Assert.Equal(1, h.Engine.HeldItemCount);

            // The event of that scene item arrives, long before the retention is over: nothing that the model knows uses the input, and nothing is held for it. The count
            // does not decide (a scene item that OBS made for the input can still be on its way): a read of the list does.
            EngineEvents.ItemRemoved(h, doomed, "Doomed", itemId, mic, "Mic");
            Assert.Equal(SourceState.Detached, Assert.Single(h.Engine.Snapshot().Inputs).State);
            var lists = 0;
            h.SettleWithTime(request =>
            {
                lists += request.Kind == ReadKind.Inputs ? 1 : 0;
                return h.World.Answer(request);
            });

            Assert.Equal(1, lists);
            Assert.Empty(h.Engine.Snapshot().Inputs);
            Assert.Equal(0, h.Engine.HeldItemCount);
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void AnInputThatIsStillHeldFor_IsLetGoOf_WhenAnEventRemovesTheSceneThatTheSceneItemsThatAreHeldForItWereIn()
        {
            Guid a = Guid.Empty;
            Guid mic = Guid.Empty;
            var h = Started(world =>
            {
                a = world.AddScene("A");
                mic = world.AddInput("Mic", "wasapi_input_capture", 2);
                world.AddItem(a, mic, "Mic");
            });

            // OBS removes the input and keeps it alive for the scene item, and then removes the scene item: a read finds it gone and holds it, and the list of inputs
            // (read for the check) has the input still.
            EngineEvents.InputRemoved(h, mic, "Mic");
            h.World.Items[a].Clear();
            h.Event("SceneItemListReindexed", new { sceneName = "A", sceneUuid = a, sceneItems = new[] { new { sceneItemId = 99, sceneItemIndex = 0 } } }, 128);
            h.SettleWithTime();
            Assert.Equal(SourceState.Detached, Assert.Single(h.Engine.Snapshot().Inputs).State);
            Assert.Equal(1, h.Engine.HeldItemCount);

            // OBS removes the scene: what a read took away from it went with it (no event tells about it), so nothing is held for the input any more. The count does not
            // decide (see above): a read of the list does.
            h.World.Scenes.RemoveAll(scene => scene.Uuid == a);
            h.World.Items.Remove(a);
            EngineEvents.SceneRemoved(h, a, "A");
            Assert.Equal(SourceState.Detached, Assert.Single(h.Engine.Snapshot().Inputs).State);
            var lists = 0;
            h.SettleWithTime(request =>
            {
                lists += request.Kind == ReadKind.Inputs ? 1 : 0;
                return h.World.Answer(request);
            });

            Assert.Equal(1, lists);
            Assert.Empty(h.Engine.Snapshot().Inputs);
            Assert.Equal(0, h.Engine.HeldItemCount);
            Assert.True(h.Engine.IsSettled);
        }

        [Theory]
        [InlineData("an event")]
        [InlineData("the answer to a read")]
        [InlineData("the look for the next read")]
        public void WhenWhatWasHeldForTheLastThingThatTheEngineWaitedForIsLetGoOf_TheEngineIsSynchronized(string whenTheTimeIsUp)
        {
            var h = new EngineHarness(new StudioEngineOptions { TombstoneRetention = Quick.TombstoneRetention, TombstoneCleanupInterval = Quick.TombstoneCleanupInterval });
            var a = h.World.AddScene("A");
            var mic = h.World.AddInput("Mic", "wasapi_input_capture", 2);
            h.World.AddItem(a, mic, "Mic");

            // OBS does not answer the reads of the audio state of the input for as long as the test lasts: the model is not complete, and the reads are repeated.
            (int Code, object? Data) Answer(ReadRequest request)
            {
                return request.Kind is ReadKind.AudioMute or ReadKind.AudioVolume ? (702, null) : h.World.Answer(request);
            }

            h.Start();
            h.Settle(Answer);
            Assert.Equal(StudioEngineState.Synchronizing, h.Engine.State);

            // OBS removes the input and keeps it alive for the scene item, and then removes the scene item: a read finds it gone and holds it, and the list of inputs
            // (read for the check) has the input still, so it stays Detached while something is held for it.
            EngineEvents.InputRemoved(h, mic, "Mic");
            h.World.Items[a].Clear();
            h.Event("SceneItemListReindexed", new { sceneName = "A", sceneUuid = a, sceneItems = new[] { new { sceneItemId = 99, sceneItemIndex = 0 } } }, 128);
            h.Settle(Answer);
            Assert.Equal(1, h.Engine.HeldItemCount);
            Assert.Equal(StudioEngineState.Synchronizing, h.Engine.State);
            Assert.False(h.Engine.IsSettled);

            // The audio state of the input is the last that the engine waits for, and the time for what was held for the input is up when the engine looks at what it
            // remembers: the input goes, and with it what the engine waited for. Whatever the message that it looks at, the engine is left in the state of its model.
            var wake = h.Engine.NextWake;
            ReadTicket? sent = null;
            if (whenTheTimeIsUp == "the answer to a read")
            {
                h.Advance(wake!.Value - h.Now);
                sent = h.Take();
                Assert.NotNull(sent);
            }

            h.Advance(TimeSpan.FromMinutes(2));
            switch (whenTheTimeIsUp)
            {
                case "an event":
                    h.Event("StudioModeStateChanged", new { studioModeEnabled = true }, 1 << 10);
                    break;
                case "the answer to a read":
                    h.Complete(sent!, Answer);
                    break;
                default:
                    Assert.Null(h.Take());
                    break;
            }

            Assert.Equal(0, h.Engine.HeldItemCount);
            Assert.True(h.Engine.IsSettled);
            Assert.Equal(StudioEngineState.Synchronized, h.Engine.State);
            Assert.Null(h.Engine.NextWake);
        }

        [Fact]
        public void TheSceneItemsThatWentWithASceneThatAReadFoundGone_AreLetGoOfByTimeWhenTheirSourceLivesOn()
        {
            Guid doomed = Guid.Empty;
            var h = Started(world =>
            {
                doomed = world.AddScene("Doomed");
                var mic = world.AddInput("Mic", "wasapi_input_capture", 2);
                world.AddItem(doomed, mic, "Mic");
            });
            h.World.Scenes.RemoveAll(scene => scene.Uuid == doomed);
            h.World.Items.Remove(doomed);
            LaterAMessageArrives(h, TimeSpan.FromSeconds(1), "Other");
            Assert.Equal(1, h.Engine.HeldItemCount);

            // The input lives on, so nothing ends what was held for it but the time.
            LaterAMessageArrives(h, TimeSpan.FromMinutes(2), "Two");

            Assert.Equal(0, h.Engine.HeldItemCount);
        }

        [Fact]
        public void AnInputThatOBSGoesOnListing_IsNeverTakenForANewOne_HoweverLongTheConnectionLasts()
        {
            var desktop = Guid.Empty;
            var h = Started(world => desktop = world.AddInput("Desktop Audio", "wasapi_output_capture", 2));
            Assert.Single(h.Engine.Snapshot().Inputs);
            h.Engine.TakeChanges();

            // OBS removes a global audio input and keeps it alive for the channel that it is assigned to: the event says it, and the list goes on having it.
            EngineEvents.InputRemoved(h, desktop, "Desktop Audio");
            h.SettleWithTime();
            Assert.Empty(h.Engine.Snapshot().Inputs);

            var lists = 0;
            for (var minute = 1; minute <= 60; minute++)
            {
                LaterAMessageArrives(h, TimeSpan.FromMinutes(1), "Scene " + minute, request =>
                {
                    lists += request.Kind == ReadKind.Inputs ? 1 : 0;
                    return h.World.Answer(request);
                });
            }

            Assert.Empty(h.Engine.Snapshot().Inputs);
            Assert.DoesNotContain(h.Engine.TakeChanges(), change => change.Kind == StudioChangeKind.InputAdded);

            // It is still remembered (that is what keeps it out), and the engine asked the list about it now and then, but not at every message.
            Assert.Equal(1, h.Engine.RememberedCount);
            Assert.InRange(lists, 10, 40);
        }

        [Fact]
        public void WhenOBSIsDoneWithAnInput_ItIsLetGoOfAfterTheRetention()
        {
            var desktop = Guid.Empty;
            var h = Started(world => desktop = world.AddInput("Desktop Audio", "wasapi_output_capture", 2));
            EngineEvents.InputRemoved(h, desktop, "Desktop Audio");
            h.SettleWithTime();
            LaterAMessageArrives(h, TimeSpan.FromMinutes(3), "One");
            Assert.Equal(1, h.Engine.RememberedCount);

            // The channel is given to another source: OBS has done with the input, and the list does not have it.
            h.World.Inputs.RemoveAll(input => input.Uuid == desktop);
            LaterAMessageArrives(h, TimeSpan.FromMinutes(3), "Two");
            Assert.Equal(1, h.Engine.RememberedCount);

            LaterAMessageArrives(h, TimeSpan.FromMinutes(3), "Three");
            Assert.Equal(0, h.Engine.RememberedCount);
        }

        [Fact]
        public void ASourceThatNoListConfirmed_GetsAReadOfItsListOnceItIsOld()
        {
            var mic = Guid.Empty;
            var h = Started(world => mic = world.AddInput("Mic", "wasapi_input_capture", 2));
            EngineEvents.InputRemoved(h, mic, "Mic");
            h.SettleWithTime();
            h.World.Inputs.Clear();

            // What the removal did needed no read of a list.
            Assert.Null(h.Take());

            // Not old yet: the scene that is made is read, and nothing else.
            var kinds = new List<ReadKind>();
            Func<ReadRequest, (int Code, object? Data)> answer = request =>
            {
                kinds.Add(request.Kind);
                return h.World.Answer(request);
            };
            LaterAMessageArrives(h, TimeSpan.FromSeconds(40), "One", answer);
            Assert.NotEmpty(kinds);
            Assert.DoesNotContain(ReadKind.Inputs, kinds);

            // Old: the list of inputs is read too.
            kinds.Clear();
            LaterAMessageArrives(h, TimeSpan.FromSeconds(40), "Two", answer);
            Assert.Contains(ReadKind.Inputs, kinds);

            // It did not have the input: that is all that the engine wanted to know, and it does not ask again.
            kinds.Clear();
            LaterAMessageArrives(h, TimeSpan.FromSeconds(40), "Three", answer);
            Assert.DoesNotContain(ReadKind.Inputs, kinds);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void WhileAListIsBeingRead_TheLookAtWhatIsOld_DoesNotAskForTheSameListAgain(bool scenes)
        {
            Guid mic = Guid.Empty;
            Guid doomed = Guid.Empty;
            var h = Started(world =>
            {
                mic = world.AddInput("Mic", "wasapi_input_capture", 2);
                doomed = world.AddScene("Doomed");
            });

            // Something that OBS destroyed and that no list confirmed: it needed no read of a list.
            if (scenes)
            {
                h.World.Scenes.RemoveAll(scene => scene.Uuid == doomed);
                h.World.Items.Remove(doomed);
                EngineEvents.SceneRemoved(h, doomed, "Doomed");
            }
            else
            {
                h.World.Inputs.RemoveAll(input => input.Uuid == mic);
                EngineEvents.InputRemoved(h, mic, "Mic");
            }

            h.SettleWithTime();
            Assert.Null(h.Take());

            // It is old now, and the list is read for another reason; while that read is under way, the engine looks at what it remembers once more.
            h.Advance(TimeSpan.FromSeconds(70));
            if (scenes)
            {
                EngineEvents.SceneCreated(h, h.World.AddScene("Other"), "Other");
            }
            else
            {
                var other = h.World.AddInput("Other");
                EngineEvents.InputCreated(h, other, "Other");
            }

            var kind = scenes ? ReadKind.Scenes : ReadKind.Inputs;
            var read = h.Take();
            Assert.NotNull(read);
            Assert.Contains(read.Requests, request => request.Kind == kind);
            h.Advance(TimeSpan.FromSeconds(15));
            h.Event("StudioModeStateChanged", new { studioModeEnabled = true }, 1 << 10);
            h.Complete(read);

            // The read that was under way was sent after the removal, and its list does not have the source: that confirms it, and a second read says nothing more.
            var next = h.Take();
            Assert.True(next is null || next.Requests.All(request => request.Kind != kind), $"The list of {(scenes ? "scenes" : "inputs")} was read again at once.");
        }

        [Fact]
        public void AListThatWasSentBeforeTheSourceWasMade_SaysNothingAboutItsRemoval()
        {
            var h = Started();
            var y = h.World.AddInput("Y", "wasapi_input_capture", 2);
            EngineEvents.InputCreated(h, y, "Y");
            var read = h.Take();
            Assert.NotNull(read);
            Assert.Contains(read.Requests, request => request.Kind == ReadKind.Inputs);

            // While the read is under way OBS makes a global audio input and removes it, and keeps it alive: the list that the read brings was made before.
            var x = Guid.NewGuid();
            EngineEvents.InputCreated(h, x, "X");
            EngineEvents.InputRemoved(h, x, "X");
            h.Complete(read);
            h.World.Inputs.Add((x, "X", "wasapi_output_capture", 2, false, 1.0));

            // That list lacked the input, which says nothing about whether OBS is done with it: it is not let go of, and the lists that come later do not bring it back.
            LaterAMessageArrives(h, TimeSpan.FromMinutes(2), "One");

            Assert.DoesNotContain(h.Engine.Snapshot().Inputs, input => input.Uuid == x);
            Assert.Equal(1, h.Engine.RememberedCount);
        }

        [Fact]
        public void TheMemory_StaysSmall_WhileScenesAreRemovedForAHalfHour()
        {
            var h = Started();
            var most = 0;

            // A scene is removed every two seconds (before the model knows it: only the removal is remembered).
            for (var i = 0; i < 900; i++)
            {
                h.Advance(TimeSpan.FromSeconds(2));
                EngineEvents.SceneRemoved(h, Guid.NewGuid(), "Gone");
                h.SettleWithTime();
                most = Math.Max(most, h.Engine.RememberedCount);
            }

            // What the last two minutes removed, and the interval between two looks, not the 900.
            Assert.InRange(most, 30, 100);
            Assert.True(h.Engine.LetGoCount > 750);
        }

        [Fact]
        public void ANewConnection_StartsWithoutWhatTheOldOneRemembered()
        {
            var h = Started();
            EngineEvents.SceneRemoved(h, Guid.NewGuid(), "Unknown");
            Assert.Equal(1, h.Engine.RememberedCount);

            h.End();
            h.Start();

            Assert.Equal(0, h.Engine.RememberedCount);
        }

        [Fact]
        public void TheOptions_MustMakeSense()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new StudioEngine(new StudioEngineOptions { TombstoneRetention = TimeSpan.Zero }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StudioEngine(new StudioEngineOptions { TombstoneCleanupInterval = TimeSpan.FromSeconds(-1) }));
            _ = new StudioEngine(new StudioEngineOptions { TombstoneCleanupInterval = TimeSpan.Zero });
        }
    }
}
