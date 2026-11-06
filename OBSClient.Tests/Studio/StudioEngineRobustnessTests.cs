namespace OBSStudioClient.Tests.Studio
{
    using OBSStudioClient.Studio.Engine;

    /// <summary>
    /// The events of OBS Studio that the tests of the engine send, in the shapes that OBS Studio sends them.
    /// </summary>
    internal static class EngineEvents
    {
        public static void SceneCreated(EngineHarness h, Guid uuid, string name)
        {
            h.Event("SceneCreated", new { sceneName = name, sceneUuid = uuid, isGroup = false }, 4);
        }

        public static void SceneRemoved(EngineHarness h, Guid uuid, string name)
        {
            h.Event("SceneRemoved", new { sceneName = name, sceneUuid = uuid, isGroup = false }, 4);
        }

        public static void InputCreated(EngineHarness h, Guid uuid, string name, uint caps = 32777)
        {
            h.Event("InputCreated", new { inputName = name, inputUuid = uuid, inputKind = "color_source_v3", unversionedInputKind = "color_source", inputKindCaps = caps, inputSettings = new { }, defaultInputSettings = new { } }, 8);
        }

        public static void InputRemoved(EngineHarness h, Guid uuid, string name)
        {
            h.Event("InputRemoved", new { inputName = name, inputUuid = uuid }, 8);
        }

        public static void ItemCreated(EngineHarness h, Guid scene, string sceneName, int id, int index, Guid source, string sourceName)
        {
            h.Event("SceneItemCreated", new { sceneName, sceneUuid = scene, sourceName, sourceUuid = source, sceneItemId = id, sceneItemIndex = index }, 128);
        }

        public static void ItemRemoved(EngineHarness h, Guid scene, string sceneName, int id, Guid source, string sourceName)
        {
            h.Event("SceneItemRemoved", new { sceneName, sceneUuid = scene, sourceName, sourceUuid = source, sceneItemId = id }, 128);
        }
    }

    /// <summary>
    /// How a value of the model decides between what an event told and what a read returned.
    /// </summary>
    public class CellTests
    {
        [Fact]
        public void AReadThatWasSentAfterAnEventArrived_IsNewerThanTheEvent()
        {
            var cell = new Cell<bool>(false, 0);

            Assert.True(cell.ApplyRead(true, 10));
            Assert.False(cell.ApplyEvent(false, 9));
            Assert.False(cell.ApplyEvent(false, 10));
            Assert.True(cell.Value);

            Assert.True(cell.ApplyEvent(false, 11));
            Assert.False(cell.Value);
        }

        [Fact]
        public void AnEventThatArrivedAfterAReadWasSent_BeatsTheRead()
        {
            var cell = new Cell<int>(1, 0);
            Assert.True(cell.ApplyEvent(2, 12));

            Assert.False(cell.ApplyRead(3, 10));
            Assert.Equal(2, cell.Value);

            // Sent after the event arrived: it shows what the event did, and what came after it.
            Assert.True(cell.ApplyRead(3, 12));
            Assert.Equal(3, cell.Value);
        }

        [Fact]
        public void AReadThatIsOlderThanOneThatWasApplied_ChangesNothing()
        {
            var cell = Cell<int>.FromRead(1, 20);

            Assert.False(cell.ApplyRead(5, 15));

            Assert.Equal(1, cell.Value);
        }

        [Fact]
        public void ACellThatAReadMade_IsCoveredByThatRead()
        {
            var cell = Cell<string>.FromRead("a", 10);

            Assert.False(cell.ApplyEvent("b", 10));

            Assert.Equal("a", cell.Value);
        }

        [Fact]
        public void AValueThatTheModelDerives_IsAppliedWhateverReadSetTheCell_AndALaterReadStillWins()
        {
            var cell = Cell<int>.FromRead(3, 20);

            Assert.True(cell.ApplyDerived(2, 20));
            Assert.Equal(2, cell.Value);

            Assert.True(cell.ApplyRead(1, 21));
            Assert.Equal(1, cell.Value);
        }
    }

    /// <summary>
    /// What the model does when what arrives is not in the order or the shape that the usual case has: removals before the scene items are known,
    /// the results of reads of a connection that ended, lists that lag, limits.
    /// </summary>
    public class StudioEngineRobustnessTests
    {
        private static EngineHarness Started(Action<FakeWorld>? setup = null, StudioEngineOptions? options = null)
        {
            var harness = new EngineHarness(options);
            harness.World.AddScene("Scene");
            setup?.Invoke(harness.World);
            harness.Start();
            harness.SettleWithTime();
            return harness;
        }

        [Fact]
        public void AnInputThatIsRemovedBeforeTheSceneItemsAreRead_IsKeptAlive_WhenASceneItemUsesIt()
        {
            var h = new EngineHarness(new StudioEngineOptions { MaxBatch = 4 });
            h.World.AddScene("Scene");
            var gaming = h.World.AddScene("Gaming");
            var cam = h.World.AddInput("Cam");
            h.World.AddItem(gaming, cam, "Cam");
            h.Start();

            // The lists are read, the scene items are not: the model cannot know what uses the input.
            h.Complete(h.Take()!);
            EngineEvents.InputRemoved(h, cam, "Cam");
            h.SettleWithTime();

            var snapshot = h.Engine.Snapshot();
            var input = Assert.Single(snapshot.Inputs);
            Assert.Equal(("Cam", SourceState.Detached), (input.Name, input.State));
            var item = Assert.Single(snapshot.Scenes.Single(scene => scene.Name == "Gaming").Items);
            Assert.Equal(cam, item.SourceUuid);
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void AnInputThatIsRemovedBeforeTheSceneItemsAreRead_IsGone_WhenNothingUsesIt()
        {
            var h = new EngineHarness(new StudioEngineOptions { MaxBatch = 4 });
            h.World.AddScene("Scene");
            var cam = h.World.AddInput("Cam");
            h.Start();
            h.Complete(h.Take()!);
            EngineEvents.InputRemoved(h, cam, "Cam");
            h.World.Inputs.RemoveAll(input => input.Uuid == cam);

            h.SettleWithTime();

            Assert.Empty(h.Engine.Snapshot().Inputs);
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void ASceneThatIsRemovedBeforeTheSceneItemsAreRead_IsKeptAlive_WhenASceneItemShowsIt()
        {
            var h = new EngineHarness(new StudioEngineOptions { MaxBatch = 4 });
            h.World.AddScene("Scene");
            var child = h.World.AddScene("Child");
            var parent = h.World.AddScene("Parent");
            h.World.AddItem(parent, child, "Child", isScene: true);
            h.Start();
            h.Complete(h.Take()!);

            // OBS keeps the scene alive for the scene item, and does not list it any more.
            EngineEvents.SceneRemoved(h, child, "Child");
            h.World.Scenes.RemoveAll(scene => scene.Uuid == child);
            h.SettleWithTime();

            var snapshot = h.Engine.Snapshot();
            Assert.DoesNotContain(snapshot.Scenes, scene => scene.Name == "Child");
            Assert.Single(snapshot.DetachedScenes, scene => scene.Uuid == child);
            Assert.Single(snapshot.Scenes.Single(scene => scene.Name == "Parent").Items);
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void ASceneThatIsRemovedBeforeTheSceneItemsAreRead_IsGone_WhenNothingShowsIt()
        {
            var h = new EngineHarness(new StudioEngineOptions { MaxBatch = 4 });
            h.World.AddScene("Scene");
            var child = h.World.AddScene("Child");
            h.Start();
            h.Complete(h.Take()!);
            EngineEvents.SceneRemoved(h, child, "Child");
            h.World.Scenes.RemoveAll(scene => scene.Uuid == child);
            h.World.Items.Remove(child);

            h.SettleWithTime();

            var snapshot = h.Engine.Snapshot();
            Assert.DoesNotContain(snapshot.Scenes, scene => scene.Name == "Child");
            Assert.Empty(snapshot.DetachedScenes);
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void AnInputThatWasRemovedBeforeTheModelKnewIt_IsKeptAlive_WhenASceneItemUsesIt()
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            var gaming = h.World.AddScene("Gaming");
            var cam = h.World.AddInput("Cam");
            h.World.AddItem(gaming, cam, "Cam");
            h.Start();

            // The event comes before the lists: the removal of something that the model does not know.
            EngineEvents.InputRemoved(h, cam, "Cam");
            h.SettleWithTime();

            var input = Assert.Single(h.Engine.Snapshot().Inputs);
            Assert.Equal(SourceState.Detached, input.State);
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void AnInputThatWasRemovedBeforeTheModelKnewIt_IsNotBroughtBack_ByALateCopyOfItsCreationOrAListThatLags()
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            var cam = h.World.AddInput("Cam");
            h.Start();
            EngineEvents.InputRemoved(h, cam, "Cam");
            EngineEvents.InputCreated(h, cam, "Cam");
            h.World.Inputs.RemoveAll(input => input.Uuid == cam);

            h.SettleWithTime();

            Assert.Empty(h.Engine.Snapshot().Inputs);
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void ASceneThatWasRemovedBeforeTheModelKnewIt_IsNotBroughtBack_ByAListThatLags()
        {
            var h = Started();
            var ghost = Guid.NewGuid();
            EngineEvents.SceneRemoved(h, ghost, "Ghost");

            // The list of scenes still has it for a while, and OBS cannot read its scene items any more.
            h.World.Scenes.Insert(0, (ghost, "Ghost"));
            h.Event("SceneListChanged", new { scenes = h.World.Scenes.Select((s, i) => new { sceneIndex = i, sceneName = s.Name, sceneUuid = s.Uuid }).ToArray() }, 4);
            h.SettleWithTime();

            Assert.DoesNotContain(h.Engine.Snapshot().Scenes, scene => scene.Name == "Ghost");
            Assert.Empty(h.Engine.Snapshot().DetachedScenes);
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void WhatWasRemovedOnOneConnection_CanBeThereOnTheNext()
        {
            Guid gaming = Guid.Empty;
            Guid mic = Guid.Empty;
            var h = Started(world =>
            {
                gaming = world.AddScene("Gaming");
                mic = world.AddInput("Mic");
                world.AddItem(gaming, mic, "Mic");
            });

            // A scene and a scene item are removed and the model hears of it.
            EngineEvents.ItemRemoved(h, gaming, "Gaming", 1, mic, "Mic");
            h.World.Items[gaming].Clear();
            h.World.Inputs.RemoveAll(input => input.Uuid == mic);
            var scene = h.World.AddScene("Other");
            EngineEvents.SceneCreated(h, scene, "Other");
            h.SettleWithTime();
            EngineEvents.SceneRemoved(h, scene, "Other");
            h.World.Scenes.RemoveAll(s => s.Uuid == scene);
            h.World.Items.Remove(scene);
            h.SettleWithTime();
            Assert.DoesNotContain(h.Engine.Snapshot().Scenes, s => s.Name == "Other");

            // The connection ends, and on the next one they are there again with the same ids: the collection that has them was loaded again.
            h.World.Scenes.Insert(0, (scene, "Other"));
            h.World.Items[scene] = [];
            h.World.Inputs.Add((mic, "Mic", "color_source_v3", 32777u, false, 1.0));
            h.World.AddItem(gaming, mic, "Mic");
            h.End();
            h.Start();
            h.SettleWithTime();

            var snapshot = h.Engine.Snapshot();
            Assert.Contains(snapshot.Scenes, s => s.Name == "Other");
            Assert.Single(snapshot.Scenes.Single(s => s.Name == "Gaming").Items);
            Assert.Contains(snapshot.Inputs, input => input.Name == "Mic");
        }

        [Fact]
        public void TheReadOfAConnectionThatEnded_MeansNothingToTheNextOne()
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            h.Start();
            var old = h.Take()!;
            h.End();
            h.Start();
            var current = h.Take()!;
            Assert.Null(h.Take());

            // A late failure of the first must not release what the second is reading.
            h.Engine.OnReadFailed(old, h.Now);
            Assert.Null(h.Take());

            // A late result of the first must not be applied.
            h.Complete(old);
            Assert.Empty(h.Engine.Snapshot().Scenes);

            h.Complete(current);
            Assert.Single(h.Engine.Snapshot().Scenes);
        }

        [Fact]
        public void AReadOfAConnectionThatEnded_IsIgnored_WhenThereIsNoConnection()
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            h.Start();
            var read = h.Take()!;
            h.End();

            h.Complete(read);
            h.Engine.OnReadFailed(read, h.Now);

            Assert.Empty(h.Engine.Snapshot().Scenes);
            Assert.Null(h.Engine.NextWake);
        }

        [Fact]
        public void WhileThereIsNoConnection_ThereIsNothingToWakeUpFor()
        {
            var h = Started();
            EngineEvents.SceneCreated(h, Guid.NewGuid(), "Phantom");
            Assert.NotNull(h.Engine.NextWake);

            h.End();

            Assert.Null(h.Engine.NextWake);
            Assert.Null(h.Take());
        }

        [Fact]
        public void TheBackOff_GrowsWhileTheListOfScenesLags()
        {
            var h = Started();
            var reads = 0;
            EngineEvents.SceneCreated(h, Guid.NewGuid(), "Phantom");

            (int, object?) Count(ReadRequest request)
            {
                if (request.Kind == ReadKind.Scenes)
                {
                    reads++;
                }

                return h.World.Answer(request);
            }

            h.SettleWithTime(Count);

            // The grace is 10 seconds; waits of 0.25, 0.5, 1, 2 seconds and then 2 seconds again need about 8 reads, not 40.
            Assert.InRange(reads, 4, 12);
            Assert.DoesNotContain(h.Engine.Snapshot().Scenes, scene => scene.Name == "Phantom");
        }

        [Fact]
        public void ASceneItemThatAnEventMade_GetsItsSourceFound_WhenItIsASceneThatOnlyTheItemKnows()
        {
            Guid parent = Guid.Empty;
            var h = Started(world => parent = world.AddScene("Parent"));

            // A scene that OBS removed and keeps alive for a scene item: the list of scenes does not have it, and the read of the items says what it is.
            var nested = Guid.NewGuid();
            h.World.Items[nested] = [];
            h.World.AddItem(parent, nested, "Nested", isScene: true);
            EngineEvents.ItemCreated(h, parent, "Parent", 1, 0, nested, "Nested");
            h.SettleWithTime();

            var snapshot = h.Engine.Snapshot();
            Assert.Single(snapshot.DetachedScenes, scene => scene.Uuid == nested);
            Assert.Single(snapshot.Scenes.Single(scene => scene.Name == "Parent").Items);
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void AReadHasNeverMoreRequestsThanTheLimit()
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            for (var i = 0; i < 44; i++)
            {
                h.World.AddScene($"Scene {i}");
            }

            for (var i = 0; i < 6; i++)
            {
                h.World.AddInput($"Mic {i}", "wasapi_input_capture", 2);
            }

            h.Start();
            var reads = 0;
            while (h.Take() is { } ticket)
            {
                Assert.True(ticket.Requests.Count <= 50, $"A read has {ticket.Requests.Count} requests.");
                h.Complete(ticket);
                reads++;
            }

            Assert.True(reads >= 2);
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void ASmallLimit_IsKept_AndEverythingIsStillRead()
        {
            var h = new EngineHarness(new StudioEngineOptions { MaxBatch = 2 });
            h.World.AddScene("Scene");
            h.World.AddScene("Gaming");
            h.World.AddInput("Mic", "wasapi_input_capture", 2);
            h.Start();

            while (h.Take() is { } ticket)
            {
                Assert.True(ticket.Requests.Count <= 2, $"A read has {ticket.Requests.Count} requests.");
                h.Complete(ticket);
            }

            Assert.True(h.Engine.IsSettled);
            Assert.Equal(2, h.Engine.Snapshot().Scenes.Count);
            Assert.Single(h.Engine.Snapshot().Inputs);
        }

        [Fact]
        public void ALimitThatCannotHoldAnAudioState_IsRefused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new StudioEngine(new StudioEngineOptions { MaxBatch = 1 }));
        }

        [Fact]
        public void AfterAReconnect_TheModelSettles_AlsoWithInputsThatHaveNoAudio()
        {
            var h = Started(world =>
            {
                world.AddInput("Color");
                world.AddInput("Mic", "wasapi_input_capture", 2);
            });
            Assert.True(h.Engine.IsSettled);

            h.End();
            h.Start();
            h.SettleWithTime();

            Assert.Equal(StudioEngineState.Synchronized, h.Engine.State);
            Assert.True(h.Engine.IsSettled);
            Assert.Null(h.Engine.NextWake);
        }

        [Fact]
        public void TheIndexesOfTheScenes_StayInOrder_WhenAPhantomSceneIsRepaired()
        {
            var h = Started(world =>
            {
                world.AddScene("B");
                world.AddScene("A");
            });
            EngineEvents.SceneCreated(h, Guid.NewGuid(), "Phantom");
            Assert.Equal(new[] { 1, 2, 3 }, h.Engine.Snapshot().Scenes.Where(scene => scene.Name != "Phantom").Select(scene => scene.Index).ToArray());

            h.SettleWithTime();

            var scenes = h.Engine.Snapshot().Scenes;
            Assert.Equal(new[] { "A", "B", "Scene" }, scenes.Select(scene => scene.Name).ToArray());
            Assert.Equal(new[] { 0, 1, 2 }, scenes.Select(scene => scene.Index).ToArray());
        }

        [Fact]
        public void ASceneThatAnItemReadInferred_AndAnEventAnnouncesLater_GetsTheGraceOfAnAnnouncedScene()
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            var parent = h.World.AddScene("Parent");
            var child = Guid.NewGuid();
            h.World.Items[child] = [];
            h.World.AddItem(parent, child, "Child", isScene: true);
            var t0 = TimeSpan.Zero;
            var listed = false;

            (int, object?) Lagging(ReadRequest request)
            {
                // The list of scenes gets the scene one and a half seconds after its event.
                if (request.Kind == ReadKind.Scenes && !listed && h.Now - t0 >= TimeSpan.FromSeconds(1.5))
                {
                    h.World.Scenes.Insert(0, (child, "Child"));
                    listed = true;
                }

                return h.World.Answer(request);
            }

            // A read of the scene items found the scene before anything else told that it exists.
            h.Start();
            h.SettleWithTime(Lagging);
            Assert.Single(h.Engine.Snapshot().DetachedScenes);
            h.Engine.TakeChanges();

            t0 = h.Now;
            EngineEvents.SceneCreated(h, child, "Child");
            h.SettleWithTime(Lagging);

            var snapshot = h.Engine.Snapshot();
            Assert.Contains(snapshot.Scenes, scene => scene.Name == "Child");
            Assert.Empty(snapshot.DetachedScenes);
            Assert.DoesNotContain(h.Engine.TakeChanges(), change => change.Kind == StudioChangeKind.Repaired);
        }

        [Fact]
        public void ASourceThatTwoScenesUse_IsNotDestroyed_WhenItsOnlyKnownSceneItemGoesBeforeTheOtherSceneIsRead()
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            var b = h.World.AddScene("B");
            var a = h.World.AddScene("A");
            var x = h.World.AddInput("X");
            var itemA = h.World.AddItem(a, x, "X");
            h.World.AddItem(b, x, "X");
            var failB = 1;

            (int, object?) Answer(ReadRequest request)
            {
                return request.Kind == ReadKind.Items && request.Target == b && failB-- > 0 ? (500, null) : h.World.Answer(request);
            }

            h.Start();
            h.Complete(h.Take()!, Answer);
            h.Complete(h.Take()!, Answer);

            // The scene items of A are known, those of B are not (that read failed): the model knows one scene item that uses X.
            EngineEvents.InputRemoved(h, x, "X");
            h.World.Items[a].Clear();
            EngineEvents.ItemRemoved(h, a, "A", itemA, x, "X");
            h.SettleWithTime(Answer);

            var snapshot = h.Engine.Snapshot();
            var input = Assert.Single(snapshot.Inputs);
            Assert.Equal(SourceState.Detached, input.State);
            Assert.Single(snapshot.Scenes.Single(scene => scene.Name == "B").Items);
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void ASceneThatOBSAnswers600For_WhileTheListStillHasIt_IsReadAgainAfterAWait_UntilTheListLetsItGo()
        {
            Guid gaming = Guid.Empty;
            var h = Started(world => gaming = world.AddScene("Gaming"));

            // OBS is removing the scene: the list still has it, and its scene items cannot be read any more.
            (int, object?) Answer(ReadRequest request)
            {
                return request.Kind == ReadKind.Items && request.Target == gaming ? (600, null) : h.World.Answer(request);
            }

            EngineEvents.ItemCreated(h, gaming, "Gaming", 9, 0, Guid.NewGuid(), "X");
            h.Settle(Answer);

            Assert.NotNull(h.Engine.NextWake);

            h.World.Scenes.RemoveAll(scene => scene.Uuid == gaming);
            h.World.Items.Remove(gaming);
            h.SettleWithTime(Answer);

            Assert.DoesNotContain(h.Engine.Snapshot().Scenes, scene => scene.Name == "Gaming");
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void AnInputThatOBSAnswers600ForTheAudioOf_WhileTheListStillHasIt_IsReadAgainAfterAWait()
        {
            var h = Started(world => world.AddInput("Mic", "wasapi_input_capture", 2));
            h.End();
            h.Start();
            var failing = true;

            (int, object?) Answer(ReadRequest request)
            {
                return request.Kind == ReadKind.AudioMute && failing ? (600, null) : h.World.Answer(request);
            }

            h.Settle(Answer);
            Assert.NotNull(h.Engine.NextWake);

            failing = false;
            h.SettleWithTime(Answer);

            Assert.True(h.Engine.IsSettled);
            Assert.Null(h.Engine.NextWake);
        }

        [Fact]
        public void ASceneThatAnItemReadFindsAndThatWasRemovedBeforeTheModelKnewIt_StaysRemoved_WhenAListThatLagsHasIt()
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            var parent = h.World.AddScene("Parent");
            var child = Guid.NewGuid();
            h.World.Items[child] = [];
            h.World.AddItem(parent, child, "Child", isScene: true);
            var listReads = 0;

            (int, object?) Answer(ReadRequest request)
            {
                // The list does not have the scene at first (OBS keeps it alive and does not list it), and then it does (it lags behind removals).
                if (request.Kind == ReadKind.Scenes && listReads++ == 1)
                {
                    h.World.Scenes.Insert(0, (child, "Child"));
                }

                return h.World.Answer(request);
            }

            h.Start();
            EngineEvents.SceneRemoved(h, child, "Child");
            h.SettleWithTime(Answer);

            var snapshot = h.Engine.Snapshot();
            Assert.DoesNotContain(snapshot.Scenes, scene => scene.Name == "Child");
            Assert.Single(snapshot.DetachedScenes, scene => scene.Uuid == child);
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void ASceneThatWasKeptAliveOnOneConnection_IsLiveOnTheNext_WhenTheListHasIt()
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            var parent = h.World.AddScene("Parent");
            var child = h.World.AddScene("Child");
            h.World.AddItem(parent, child, "Child", isScene: true);
            h.Start();
            h.SettleWithTime();
            EngineEvents.SceneRemoved(h, child, "Child");
            h.World.Scenes.RemoveAll(scene => scene.Uuid == child);
            h.SettleWithTime();
            Assert.Single(h.Engine.Snapshot().DetachedScenes, scene => scene.Uuid == child);

            // OBS is started again from a collection that has the scene as a scene of its own.
            h.End();
            h.World.Scenes.Insert(0, (child, "Child"));
            h.Start();
            h.SettleWithTime();

            var snapshot = h.Engine.Snapshot();
            Assert.Contains(snapshot.Scenes, scene => scene.Name == "Child");
            Assert.Empty(snapshot.DetachedScenes);
        }

        [Fact]
        public void AnInputThatWasKeptAliveOnOneConnection_IsLiveOnTheNext_WhenNothingUsesItAnyMore()
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            var gaming = h.World.AddScene("Gaming");
            var mic = h.World.AddInput("Mic");
            h.World.AddItem(gaming, mic, "Mic");
            h.Start();
            h.SettleWithTime();
            EngineEvents.InputRemoved(h, mic, "Mic");
            h.SettleWithTime();
            Assert.Equal(SourceState.Detached, Assert.Single(h.Engine.Snapshot().Inputs).State);

            // OBS is started again from a collection where the input is on its own.
            h.End();
            h.World.Items[gaming].Clear();
            h.Start();
            h.SettleWithTime();

            Assert.Equal(SourceState.Live, Assert.Single(h.Engine.Snapshot().Inputs).State);
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void ASceneThatTheListLacks_IsNotDestroyedByCounting_WhenTheEventsOfItsSceneItemsAreStillOnTheirWay()
        {
            Guid host = Guid.Empty;
            Guid nested = Guid.Empty;
            var h = Started(world =>
            {
                host = world.AddScene("Host");
                nested = world.AddScene("Nested");
            });

            // In OBS a scene item of Host uses Nested, and Nested is removed, which the list shows at once. The events of both are still on
            // their way, so the model counts no scene item that uses Nested, and a list that lacks it comes first.
            var id = h.World.AddItem(host, nested, "Nested", isScene: true);
            h.World.Scenes.RemoveAll(scene => scene.Uuid == nested);
            var other = h.World.AddScene("Other");
            EngineEvents.SceneCreated(h, other, "Other");
            h.SettleWithTime();

            // The list does not say whether OBS destroyed it. A read of its scene items does: OBS has it.
            Assert.Single(h.Engine.Snapshot().DetachedScenes, scene => scene.Uuid == nested);
            Assert.True(h.Engine.IsSettled);

            EngineEvents.ItemCreated(h, host, "Host", id, 0, nested, "Nested");
            EngineEvents.SceneRemoved(h, nested, "Nested");
            h.SettleWithTime();

            var snapshot = h.Engine.Snapshot();
            Assert.Single(snapshot.DetachedScenes, scene => scene.Uuid == nested);
            Assert.Equal(nested, Assert.Single(snapshot.Scenes.Single(scene => scene.Uuid == host).Items).SourceUuid);
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void ASceneThatTheListLacks_IsDestroyed_WhenOBSDoesNotHaveIt()
        {
            Guid nested = Guid.Empty;
            var h = Started(world => nested = world.AddScene("Nested"));

            h.World.Scenes.RemoveAll(scene => scene.Uuid == nested);
            h.World.Items.Remove(nested);
            var other = h.World.AddScene("Other");
            EngineEvents.SceneCreated(h, other, "Other");
            h.SettleWithTime();

            var snapshot = h.Engine.Snapshot();
            Assert.Empty(snapshot.DetachedScenes);
            Assert.DoesNotContain(snapshot.Scenes, scene => scene.Uuid == nested);
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void AnInputThatTheListLacks_DoesNotExist_WhateverTheSceneItemsThatTheModelKnowsSay()
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            var gaming = h.World.AddScene("Gaming");
            var mic = h.World.AddInput("Mic");
            var id = h.World.AddItem(gaming, mic, "Mic");
            h.Start();
            h.SettleWithTime();

            // OBS has destroyed it, and the model still knows the scene item that used it: the event of its removal is on its way.
            h.World.Inputs.RemoveAll(input => input.Uuid == mic);
            h.World.Items[gaming].Clear();
            EngineEvents.InputCreated(h, h.World.AddInput("Cam"), "Cam");
            h.SettleWithTime();

            Assert.DoesNotContain(h.Engine.Snapshot().Inputs, input => input.Uuid == mic);

            EngineEvents.ItemRemoved(h, gaming, "Gaming", id, mic, "Mic");
            h.SettleWithTime();
            Assert.Empty(h.Engine.Snapshot().Scenes.Single(scene => scene.Uuid == gaming).Items);
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void ADetachedInputWhoseLastKnownSceneItemIsGoneInARead_IsKeptAlive_WhenAnotherSceneItemUsesItThatTheModelDoesNotKnow()
        {
            var h = new EngineHarness();
            var scene = h.World.AddScene("Scene");
            var gaming = h.World.AddScene("Gaming");
            var mic = h.World.AddInput("Mic");
            h.World.AddItem(gaming, mic, "Mic");
            h.Start();
            h.SettleWithTime();
            EngineEvents.InputRemoved(h, mic, "Mic");
            h.SettleWithTime();
            Assert.Equal(SourceState.Detached, Assert.Single(h.Engine.Snapshot().Inputs).State);

            // OBS: another scene item uses the removed input (its event is on its way), and the one that the model knows is removed. The read
            // of the scene items shows the second, and the model counts no other scene item.
            var other = h.World.AddItem(scene, mic, "Mic");
            h.World.Items[gaming].Clear();
            h.Event("SceneItemEnableStateChanged", new { sceneName = "Gaming", sceneUuid = gaming, sceneItemId = 99, sceneItemEnabled = true }, 128);
            h.SettleWithTime();

            // The list of inputs says that OBS still has it.
            Assert.Equal(SourceState.Detached, Assert.Single(h.Engine.Snapshot().Inputs).State);
            Assert.True(h.Engine.IsSettled);

            EngineEvents.ItemCreated(h, scene, "Scene", other, 0, mic, "Mic");
            h.SettleWithTime();
            Assert.Equal(mic, Assert.Single(h.Engine.Snapshot().Scenes.Single(s => s.Uuid == scene).Items).SourceUuid);
            Assert.Equal(SourceState.Detached, Assert.Single(h.Engine.Snapshot().Inputs).State);
        }

        [Fact]
        public void ADetachedInputWhoseLastSceneItemIsGoneInARead_IsDestroyed_WhenTheListOfInputsDoesNotHaveIt()
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            var gaming = h.World.AddScene("Gaming");
            var mic = h.World.AddInput("Mic");
            h.World.AddItem(gaming, mic, "Mic");
            h.Start();
            h.SettleWithTime();
            EngineEvents.InputRemoved(h, mic, "Mic");
            h.SettleWithTime();
            Assert.Equal(SourceState.Detached, Assert.Single(h.Engine.Snapshot().Inputs).State);

            h.World.Items[gaming].Clear();
            h.World.Inputs.RemoveAll(input => input.Uuid == mic);
            h.Event("SceneItemEnableStateChanged", new { sceneName = "Gaming", sceneUuid = gaming, sceneItemId = 99, sceneItemEnabled = true }, 128);
            h.SettleWithTime();

            Assert.Empty(h.Engine.Snapshot().Inputs);
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void ADetachedSceneThatOBSDestroyedWithoutAnEvent_IsDropped_WhenItsSceneItemsAreRead()
        {
            Guid host = Guid.Empty;
            Guid nested = Guid.Empty;
            var h = Started(world =>
            {
                host = world.AddScene("Host");
                nested = world.AddScene("Nested");
                world.AddItem(host, nested, "Nested", isScene: true);
            });
            h.World.Scenes.RemoveAll(scene => scene.Uuid == nested);
            EngineEvents.SceneRemoved(h, nested, "Nested");
            h.SettleWithTime();
            Assert.Single(h.Engine.Snapshot().DetachedScenes, scene => scene.Uuid == nested);

            // The scene item that kept it alive is removed, which OBS does not announce for the items of a scene that is not in the list of
            // scenes, and OBS destroys the scene. An event about one of its scene items makes the model read them: OBS answers 600.
            h.World.Items.Remove(nested);
            h.World.Items[host].Clear();
            h.Event("SceneItemEnableStateChanged", new { sceneName = "Nested", sceneUuid = nested, sceneItemId = 99, sceneItemEnabled = true }, 128);
            h.SettleWithTime();

            Assert.Empty(h.Engine.Snapshot().DetachedScenes);
            Assert.True(h.Engine.IsSettled);
        }

        // Seed 5190 of the random walks (Hostile): a scene that a scene item showed, and that no list of scenes had yet (the list lags behind a creation),
        // was destroyed by the count when the only scene item that the model knew of went with its scene. Another scene item that the model did not
        // know yet used it: OBS kept it alive, the model had it as gone for good, and the list of scenes does not bring back what the model destroyed.
        [Fact]
        public void ASceneThatAnItemReadInferred_IsNotDestroyed_WhenItsOnlyKnownSceneItemGoesWithItsScene_WhileAnotherUsesItThatTheModelDoesNotKnow()
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            var parent = h.World.AddScene("Parent");
            var child = Guid.NewGuid();
            h.World.Items[child] = [];
            h.World.AddItem(parent, child, "Child", isScene: true);
            h.Start();
            h.SettleWithTime();
            Assert.Equal(child, Assert.Single(h.Engine.Snapshot().DetachedScenes).Uuid);
            h.Engine.TakeChanges();

            // OBS: another scene shows the child too, and the parent goes with its scene item. The model reads the parent first (OBS answers 600: it is
            // destroyed), and the scene items of the other scene only afterwards.
            var other = h.World.AddScene("Other");
            h.World.AddItem(other, child, "Child", isScene: true);
            EngineEvents.SceneCreated(h, other, "Other");
            h.World.Scenes.RemoveAll(scene => scene.Uuid == parent);
            h.World.Items.Remove(parent);
            EngineEvents.SceneRemoved(h, parent, "Parent");
            h.Event("SceneItemEnableStateChanged", new { sceneName = "Parent", sceneUuid = parent, sceneItemId = 99, sceneItemEnabled = true }, 128);
            var failures = 1;

            (int, object?) Answer(ReadRequest request)
            {
                return request.Kind == ReadKind.Items && request.Target == other && failures-- > 0 ? (500, null) : h.World.Answer(request);
            }

            h.SettleWithTime(Answer);

            var snapshot = h.Engine.Snapshot();
            Assert.Equal(child, Assert.Single(snapshot.DetachedScenes).Uuid);
            Assert.Equal(child, Assert.Single(snapshot.Scenes.Single(scene => scene.Uuid == other).Items).SourceUuid);
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void ASceneThatAnItemReadInferred_ThatOBSKeepsAlive_IsDestroyedWhenAnEventRemovesIt_AndNoSceneItemUsesIt()
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            var parent = h.World.AddScene("Parent");
            var child = Guid.NewGuid();
            h.World.Items[child] = [];
            var id = h.World.AddItem(parent, child, "Child", isScene: true);
            h.Start();
            h.SettleWithTime();
            Assert.Equal(child, Assert.Single(h.Engine.Snapshot().DetachedScenes).Uuid);

            // The scene item that showed the scene goes. OBS has the scene still (it is not removed, and the list of scenes is late), so the model keeps it.
            h.World.Items[parent].Clear();
            EngineEvents.ItemRemoved(h, parent, "Parent", id, child, "Child");
            h.SettleWithTime();
            Assert.Equal(child, Assert.Single(h.Engine.Snapshot().DetachedScenes).Uuid);
            h.Engine.TakeChanges();

            // OBS removes it now, and nothing shows it: it is destroyed, and the model must not hold a scene that OBS does not have.
            h.World.Items.Remove(child);
            EngineEvents.SceneRemoved(h, child, "Child");
            h.SettleWithTime();

            Assert.Empty(h.Engine.Snapshot().DetachedScenes);
            Assert.Contains(h.Engine.TakeChanges(), change => change.Kind == StudioChangeKind.SceneRemoved && change.Subject == child);
            Assert.True(h.Engine.IsSettled);
        }

        // A read of the scene items of a scene that the model doubted, taken before OBS removed the scene, says that OBS had it then: it says nothing of what
        // OBS did with it when it removed it. The answer arrives after the event of the removal, and must not be taken for a word about that.
        [Fact]
        public void ASceneThatAnItemReadInferred_AndThatOBSRemovesWhileACheckOfItIsUnderWay_IsNotKeptByTheLateAnswerOfThatCheck()
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            var parent = h.World.AddScene("Parent");
            var child = Guid.NewGuid();
            h.World.Items[child] = [];
            var id = h.World.AddItem(parent, child, "Child", isScene: true);
            h.Start();
            h.SettleWithTime();
            Assert.Equal(child, Assert.Single(h.Engine.Snapshot().DetachedScenes).Uuid);
            h.Engine.TakeChanges();

            // The model cannot count every scene item yet: a scene was created, and its scene items are not read. The scene item that showed the child
            // goes, so the model checks the child, and the read is taken.
            var other = h.World.AddScene("Other");
            EngineEvents.SceneCreated(h, other, "Other");
            h.World.Items[parent].Clear();
            EngineEvents.ItemRemoved(h, parent, "Parent", id, child, "Child");
            var check = h.Take();
            Assert.NotNull(check);
            Assert.Contains(check.Requests, request => request.Kind == ReadKind.Items && request.Target == child);
            var answers = check.Requests.ToDictionary(request => request, request => h.World.Answer(request));

            // OBS removes the child, which nothing shows, and destroys it; the event comes, and then the answer of the read that was taken before.
            h.World.Items.Remove(child);
            EngineEvents.SceneRemoved(h, child, "Child");
            h.Complete(check, request => answers[request]);
            h.SettleWithTime();

            Assert.Empty(h.Engine.Snapshot().DetachedScenes);
            Assert.Contains(h.Engine.TakeChanges(), change => change.Kind == StudioChangeKind.SceneRemoved && change.Subject == child);
            Assert.True(h.Engine.IsSettled);
        }
    }
}
