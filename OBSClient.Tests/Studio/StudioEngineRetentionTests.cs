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
