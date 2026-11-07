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

        // A read of the scene items of a scene that the model doubted, taken before OBS removed the scene, says that OBS had the scene then: it says nothing of
        // what OBS did with it when it removed it. The answer arrives after the event of the removal, and must not be taken for a word about that. The creation
        // event of the scene can come in between (OBS sends it late, and the model had only inferred the scene), which makes the scene Live.
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ASceneThatAnItemReadInferred_AndThatOBSRemovesWhileACheckOfItIsUnderWay_IsNotKeptByTheLateAnswerOfThatCheck(bool itsCreationEventComesFirst)
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
            if (itsCreationEventComesFirst)
            {
                EngineEvents.SceneCreated(h, child, "Child");
            }

            EngineEvents.SceneRemoved(h, child, "Child");
            h.Complete(check, request => answers[request]);
            h.SettleWithTime();

            Assert.Empty(h.Engine.Snapshot().DetachedScenes);
            Assert.Contains(h.Engine.TakeChanges(), change => change.Kind == StudioChangeKind.SceneRemoved && change.Subject == child);
            Assert.True(h.Engine.IsSettled);
        }

        // Seed 10034 of the random walks (Hostile): the list of scenes lacked a scene that OBS had removed and that a scene item keeps alive, so the model had
        // it Detached because of a read, which is ahead of the events. The event that removed another scene took away the only scene item that the model knew
        // to show it, and the model destroyed it by counting. A scene item that was made before the removal, and whose event was still on its way, showed it:
        // OBS kept it, with its scene items and the inputs that only they used, and the model had destroyed all of that.
        [Fact]
        public void ASceneThatTheListLacks_IsNotDestroyedByCounting_WhenAnOlderEventTakesAwayTheOnlySceneItemThatTheModelKnows()
        {
            Guid shown = Guid.Empty;
            Guid first = Guid.Empty;
            Guid second = Guid.Empty;
            Guid mic = Guid.Empty;
            var h = Started(world =>
            {
                shown = world.AddScene("Shown");
                first = world.AddScene("First");
                second = world.AddScene("Second");
                mic = world.AddInput("Mic", "wasapi_input_capture", 2);
                world.AddItem(shown, mic, "Mic");
                world.AddItem(first, shown, "Shown", isScene: true);
            });

            // The input was removed: the scene item in Shown keeps it alive.
            EngineEvents.InputRemoved(h, mic, "Mic");
            h.SettleWithTime();
            Assert.Equal(SourceState.Detached, h.Engine.Snapshot().Inputs.Single(input => input.Uuid == mic).State);

            // In OBS Second gets a scene item that shows Shown, and Shown is removed (the list lacks it at once, and the scene items keep it alive). The events are
            // still on their way, and a list of scenes is read first.
            var secondItem = h.World.AddItem(second, shown, "Shown", isScene: true);
            h.World.Scenes.RemoveAll(scene => scene.Uuid == shown);
            var other = h.World.AddScene("Other");
            EngineEvents.SceneCreated(h, other, "Other");
            h.SettleWithTime();
            Assert.Single(h.Engine.Snapshot().DetachedScenes, scene => scene.Uuid == shown);

            // An older event: OBS removed First, and its scene item that showed Shown, before all this. The model knows no other scene item that shows Shown.
            h.World.Scenes.RemoveAll(scene => scene.Uuid == first);
            h.World.Items.Remove(first);
            EngineEvents.SceneRemoved(h, first, "First");
            h.SettleWithTime();

            // A read says that OBS has Shown still, with its scene item and its input.
            var snapshot = h.Engine.Snapshot();
            var kept = Assert.Single(snapshot.DetachedScenes, scene => scene.Uuid == shown);
            Assert.Equal(mic, Assert.Single(kept.Items).SourceUuid);
            Assert.Equal(SourceState.Detached, snapshot.Inputs.Single(input => input.Uuid == mic).State);

            // The events of what happened before come: the scene item in Second, and the removal of Shown.
            EngineEvents.ItemCreated(h, second, "Second", secondItem, 0, shown, "Shown");
            EngineEvents.SceneRemoved(h, shown, "Shown");
            h.SettleWithTime();

            snapshot = h.Engine.Snapshot();
            Assert.Single(snapshot.DetachedScenes, scene => scene.Uuid == shown);
            Assert.Equal(shown, Assert.Single(snapshot.Scenes.Single(scene => scene.Uuid == second).Items).SourceUuid);
            Assert.Equal(SourceState.Detached, snapshot.Inputs.Single(input => input.Uuid == mic).State);
            Assert.True(h.Engine.IsSettled);
        }

        // The event that removes a scene that a read found Detached says where in the order of what happened OBS removed it: the scene items that the model
        // knows then decide what OBS did with it. Nothing shows it, so it is destroyed at once, and the answer of a check that was taken before is not waited for.
        [Fact]
        public void ASceneThatTheListLacked_IsDestroyedByTheEventThatRemovesIt_WhenNoSceneItemShowsIt()
        {
            Guid gone = Guid.Empty;
            var h = Started(world => gone = world.AddScene("Gone"));
            h.World.Scenes.RemoveAll(scene => scene.Uuid == gone);
            h.World.Items.Remove(gone);
            var other = h.World.AddScene("Other");
            EngineEvents.SceneCreated(h, other, "Other");

            // The list of scenes lacks it, so the model doubts whether OBS has it: the read that says is taken, and not answered yet.
            h.Complete(h.Take()!);
            var check = h.Take();
            Assert.NotNull(check);
            Assert.Contains(check.Requests, request => request.Kind == ReadKind.Items && request.Target == gone);
            Assert.Single(h.Engine.Snapshot().DetachedScenes, scene => scene.Uuid == gone);
            h.Engine.TakeChanges();

            EngineEvents.SceneRemoved(h, gone, "Gone");

            Assert.Empty(h.Engine.Snapshot().DetachedScenes);
            Assert.Contains(h.Engine.TakeChanges(), change => change.Kind == StudioChangeKind.SceneRemoved && change.Subject == gone);
            h.Complete(check);
            h.SettleWithTime();
            Assert.Empty(h.Engine.Snapshot().DetachedScenes);
            Assert.True(h.Engine.IsSettled);
        }

        // From the review of the random walks: a read is ahead of the events. OBS removed C while the scene item of A showed it. Then a scene item of B was made
        // that shows it (OBS finds a source that it keeps alive by its UUID), and then the scene item of A was taken away. The model read the scene items of A
        // (and the list of scenes, in the second case) before any of these events arrived: A is empty, C is not in the list, and nothing is known of the scene
        // item of B. The scene item of A is gone as far as the model knows, and it was there when OBS removed C: counting the scene items that the model knows
        // when the event that removes C arrives says that nothing shows it, and destroys it for good. The scene item of B is made later, and its source never
        // comes back.
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ASceneThatOBSRemovedWhileASceneItemShowedIt_IsNotDestroyedByCounting_WhenAReadTookThatSceneItemAwayAndAnotherOneIsOnItsWay(bool theListOfScenesWasReadToo)
        {
            Guid a = Guid.Empty;
            Guid b = Guid.Empty;
            Guid c = Guid.Empty;
            var firstItem = 0;
            var h = Started(world =>
            {
                a = world.AddScene("A");
                b = world.AddScene("B");
                c = world.AddScene("C");
                firstItem = world.AddItem(a, c, "C", isScene: true);
            });

            // What OBS did, in this order: removed C (A shows it, so it stays alive), made a scene item of B that shows C, took away the scene item of A.
            h.World.Scenes.RemoveAll(scene => scene.Uuid == c);
            var secondItem = h.World.AddItem(b, c, "C", isScene: true);
            h.World.Items[a].Clear();

            // Meanwhile the scene items of A are read, and the list of scenes in the second case. None of the events has arrived.
            h.Event("SceneItemListReindexed", new { sceneName = "A", sceneUuid = a, sceneItems = new[] { new { sceneItemId = 99, sceneItemIndex = 0 } } }, 128);
            if (theListOfScenesWasReadToo)
            {
                EngineEvents.SceneCreated(h, h.World.AddScene("Other"), "Other");
            }

            h.SettleWithTime();
            Assert.Empty(h.Engine.Snapshot().Scenes.Single(scene => scene.Uuid == a).Items);
            Assert.Empty(h.Engine.Snapshot().Scenes.Single(scene => scene.Uuid == b).Items);

            // The events, in the order in which they happened.
            EngineEvents.SceneRemoved(h, c, "C");
            EngineEvents.ItemCreated(h, b, "B", secondItem, 0, c, "C");
            EngineEvents.ItemRemoved(h, a, "A", firstItem, c, "C");
            h.SettleWithTime();

            var snapshot = h.Engine.Snapshot();
            Assert.Single(snapshot.DetachedScenes, scene => scene.Uuid == c);
            Assert.Equal(c, Assert.Single(snapshot.Scenes.Single(scene => scene.Uuid == b).Items).SourceUuid);
            Assert.True(h.Engine.IsSettled);
        }

        // The same for an input.
        [Fact]
        public void AnInputThatOBSRemovedWhileASceneItemUsedIt_IsNotDestroyedByCounting_WhenAReadTookThatSceneItemAwayAndAnotherOneIsOnItsWay()
        {
            Guid a = Guid.Empty;
            Guid b = Guid.Empty;
            Guid mic = Guid.Empty;
            var firstItem = 0;
            var h = Started(world =>
            {
                a = world.AddScene("A");
                b = world.AddScene("B");
                mic = world.AddInput("Mic", "wasapi_input_capture", 2);
                firstItem = world.AddItem(a, mic, "Mic");
            });

            // OBS removed the input (A uses it, so it stays alive, and the list of inputs has it), made a scene item of B that uses it, took away the one of A.
            var secondItem = h.World.AddItem(b, mic, "Mic");
            h.World.Items[a].Clear();
            h.Event("SceneItemListReindexed", new { sceneName = "A", sceneUuid = a, sceneItems = new[] { new { sceneItemId = 99, sceneItemIndex = 0 } } }, 128);
            h.SettleWithTime();
            Assert.Empty(h.Engine.Snapshot().Scenes.Single(scene => scene.Uuid == a).Items);

            EngineEvents.InputRemoved(h, mic, "Mic");
            EngineEvents.ItemCreated(h, b, "B", secondItem, 0, mic, "Mic");
            EngineEvents.ItemRemoved(h, a, "A", firstItem, mic, "Mic");
            h.SettleWithTime();

            var snapshot = h.Engine.Snapshot();
            Assert.Equal(SourceState.Detached, snapshot.Inputs.Single(input => input.Uuid == mic).State);
            Assert.Equal(mic, Assert.Single(snapshot.Scenes.Single(scene => scene.Uuid == b).Items).SourceUuid);
            Assert.True(h.Engine.IsSettled);
        }

        // What a read takes away makes the model ask OBS, it does not make it keep what OBS does not have: when no other scene item shows the scene, OBS
        // destroyed it, and the read says so.
        [Fact]
        public void ASceneThatOBSRemovedWhileASceneItemShowedIt_IsDestroyed_WhenAReadTookThatSceneItemAwayAndNothingElseShowsIt()
        {
            Guid a = Guid.Empty;
            Guid c = Guid.Empty;
            var firstItem = 0;
            var h = Started(world =>
            {
                a = world.AddScene("A");
                c = world.AddScene("C");
                firstItem = world.AddItem(a, c, "C", isScene: true);
            });

            h.World.Scenes.RemoveAll(scene => scene.Uuid == c);
            h.World.Items.Remove(c);
            h.World.Items[a].Clear();
            h.Event("SceneItemListReindexed", new { sceneName = "A", sceneUuid = a, sceneItems = new[] { new { sceneItemId = 99, sceneItemIndex = 0 } } }, 128);
            h.SettleWithTime();

            EngineEvents.SceneRemoved(h, c, "C");
            EngineEvents.ItemRemoved(h, a, "A", firstItem, c, "C");
            h.SettleWithTime();

            var snapshot = h.Engine.Snapshot();
            Assert.DoesNotContain(snapshot.Scenes, scene => scene.Uuid == c);
            Assert.Empty(snapshot.DetachedScenes);
            Assert.True(h.Engine.IsSettled);
        }

        // The scene item that goes is not the only one that OBS had made: another one was taken away, a read told so, and its event has not come. OBS made a
        // new scene item that uses the input after the one that goes was removed. The model knows no other scene item, but it does not conclude that OBS
        // destroyed the input.
        [Fact]
        public void AnInputThatOBSRemoved_IsNotDestroyedByCounting_WhenTheLastSceneItemThatTheModelKnowsGoesWhileAReadHasTakenAnotherAway()
        {
            Guid a = Guid.Empty;
            Guid b = Guid.Empty;
            Guid third = Guid.Empty;
            Guid mic = Guid.Empty;
            var itemOfA = 0;
            var itemOfB = 0;
            var h = Started(world =>
            {
                a = world.AddScene("A");
                b = world.AddScene("B");
                third = world.AddScene("Third");
                mic = world.AddInput("Mic", "wasapi_input_capture", 2);
                itemOfA = world.AddItem(a, mic, "Mic");
                itemOfB = world.AddItem(b, mic, "Mic");
            });
            EngineEvents.InputRemoved(h, mic, "Mic");
            h.SettleWithTime();
            Assert.Equal(SourceState.Detached, h.Engine.Snapshot().Inputs.Single(input => input.Uuid == mic).State);

            // OBS took away the scene item of A (that event comes first), made a scene item of Third that uses the input, and took away the one of B. The read
            // of B is ahead of all of it.
            h.World.Items[a].Clear();
            var itemOfThird = h.World.AddItem(third, mic, "Mic");
            h.World.Items[b].Clear();
            h.Event("SceneItemListReindexed", new { sceneName = "B", sceneUuid = b, sceneItems = new[] { new { sceneItemId = 99, sceneItemIndex = 0 } } }, 128);
            h.SettleWithTime();

            EngineEvents.ItemRemoved(h, a, "A", itemOfA, mic, "Mic");
            EngineEvents.ItemCreated(h, third, "Third", itemOfThird, 0, mic, "Mic");
            EngineEvents.ItemRemoved(h, b, "B", itemOfB, mic, "Mic");
            h.SettleWithTime();

            var snapshot = h.Engine.Snapshot();
            Assert.Equal(SourceState.Detached, snapshot.Inputs.Single(input => input.Uuid == mic).State);
            Assert.Equal(mic, Assert.Single(snapshot.Scenes.Single(scene => scene.Uuid == third).Items).SourceUuid);
            Assert.True(h.Engine.IsSettled);
        }

        // The event that a read was ahead of takes the scene item out of what is held: from then on the count is right again, and the last scene item that
        // goes destroys the input at once, without a read.
        [Fact]
        public void AnInputThatOBSRemoved_IsDestroyedByCountingAgain_WhenTheEventOfTheSceneItemThatAReadTookAwayHasArrived()
        {
            Guid a = Guid.Empty;
            Guid b = Guid.Empty;
            Guid mic = Guid.Empty;
            var itemOfA = 0;
            var itemOfB = 0;
            var h = Started(world =>
            {
                a = world.AddScene("A");
                b = world.AddScene("B");
                mic = world.AddInput("Mic", "wasapi_input_capture", 2);
                itemOfA = world.AddItem(a, mic, "Mic");
                itemOfB = world.AddItem(b, mic, "Mic");
            });
            EngineEvents.InputRemoved(h, mic, "Mic");
            h.SettleWithTime();

            // OBS took away the scene item of A, and a read of A tells before the event does. The scene item of B still keeps the input alive.
            h.World.Items[a].Clear();
            h.Event("SceneItemListReindexed", new { sceneName = "A", sceneUuid = a, sceneItems = new[] { new { sceneItemId = 99, sceneItemIndex = 0 } } }, 128);
            h.SettleWithTime();
            Assert.Equal(SourceState.Detached, h.Engine.Snapshot().Inputs.Single(input => input.Uuid == mic).State);

            // The event arrives. Then OBS takes away the scene item of B, and destroys the input: the model knows it from the count.
            EngineEvents.ItemRemoved(h, a, "A", itemOfA, mic, "Mic");
            EngineEvents.ItemRemoved(h, b, "B", itemOfB, mic, "Mic");

            Assert.DoesNotContain(h.Engine.Snapshot().Inputs, input => input.Uuid == mic);
        }

        // The scene items of a scene that goes are not announced one by one: what a read took away from it, and that no event told about, is let go of with the
        // scene. The scene item of the other scene that goes later then destroys the input by counting, at once.
        [Fact]
        public void TheSceneItemsThatAReadTookAwayFromASceneThatGoes_NoLongerHoldAnInput()
        {
            Guid a = Guid.Empty;
            Guid doomed = Guid.Empty;
            Guid mic = Guid.Empty;
            var itemOfA = 0;
            var h = Started(world =>
            {
                a = world.AddScene("A");
                doomed = world.AddScene("Doomed");
                mic = world.AddInput("Mic", "wasapi_input_capture", 2);
                itemOfA = world.AddItem(a, mic, "Mic");
                world.AddItem(doomed, mic, "Mic");
            });
            EngineEvents.InputRemoved(h, mic, "Mic");
            h.SettleWithTime();

            // OBS took away the scene item of Doomed, and a read of Doomed tells (the scene item of A keeps the input alive).
            h.World.Items[doomed].Clear();
            h.Event("SceneItemListReindexed", new { sceneName = "Doomed", sceneUuid = doomed, sceneItems = new[] { new { sceneItemId = 99, sceneItemIndex = 0 } } }, 128);
            h.SettleWithTime();
            Assert.Equal(SourceState.Detached, h.Engine.Snapshot().Inputs.Single(input => input.Uuid == mic).State);

            // Doomed goes: nothing shows it, and nothing more is told about its scene items. Then the scene item of A goes: the input is destroyed.
            h.World.Scenes.RemoveAll(scene => scene.Uuid == doomed);
            h.World.Items.Remove(doomed);
            EngineEvents.SceneRemoved(h, doomed, "Doomed");
            Assert.DoesNotContain(h.Engine.Snapshot().Scenes, scene => scene.Uuid == doomed);
            Assert.Empty(h.Engine.Snapshot().DetachedScenes);
            EngineEvents.ItemRemoved(h, a, "A", itemOfA, mic, "Mic");

            Assert.DoesNotContain(h.Engine.Snapshot().Inputs, input => input.Uuid == mic);
        }

        // OBS removed the input while the model did not know all scene items (a scene had appeared that it had not read), so it decides when it does. By then a
        // read has taken away the only scene item that it knew, and its event has not come: the count says that nothing uses the input, and a scene item that
        // OBS made after the removal does.
        [Fact]
        public void AnInputThatOBSRemovedWhileNotAllSceneItemsWereKnown_IsNotDestroyedByCounting_WhenAReadTookTheOnlyKnownSceneItemAwayAndAnotherOneIsOnItsWay()
        {
            Guid a = Guid.Empty;
            Guid b = Guid.Empty;
            Guid mic = Guid.Empty;
            var itemOfA = 0;
            var h = Started(world =>
            {
                a = world.AddScene("A");
                b = world.AddScene("B");
                mic = world.AddInput("Mic", "wasapi_input_capture", 2);
                itemOfA = world.AddItem(a, mic, "Mic");
            });

            h.World.Items[a].Clear();
            h.Event("SceneItemListReindexed", new { sceneName = "A", sceneUuid = a, sceneItems = new[] { new { sceneItemId = 99, sceneItemIndex = 0 } } }, 128);
            h.SettleWithTime();
            Assert.Empty(h.Engine.Snapshot().Scenes.Single(scene => scene.Uuid == a).Items);

            // A scene that the model has not read appeared, and the input was removed: until its scene items are known the model cannot tell what OBS did.
            EngineEvents.SceneCreated(h, h.World.AddScene("New"), "New");
            EngineEvents.InputRemoved(h, mic, "Mic");
            var itemOfB = h.World.AddItem(b, mic, "Mic");
            h.SettleWithTime();
            Assert.Equal(SourceState.Detached, h.Engine.Snapshot().Inputs.Single(input => input.Uuid == mic).State);

            EngineEvents.ItemRemoved(h, a, "A", itemOfA, mic, "Mic");
            EngineEvents.ItemCreated(h, b, "B", itemOfB, 0, mic, "Mic");
            h.SettleWithTime();

            var snapshot = h.Engine.Snapshot();
            Assert.Equal(SourceState.Detached, snapshot.Inputs.Single(input => input.Uuid == mic).State);
            Assert.Equal(mic, Assert.Single(snapshot.Scenes.Single(scene => scene.Uuid == b).Items).SourceUuid);
            Assert.True(h.Engine.IsSettled);
        }

        // The same, when the check of the list of inputs is done between the event that removes the only scene item that the model knew and the event that makes the
        // other one: the list says that OBS has the input, and not who uses it, so a count of nothing does not destroy it (the event of the other scene item is on its way).
        [Fact]
        public void AnInputThatOBSRemoved_IsNotDestroyedByCounting_WhenItsCheckIsDoneBetweenTheEventThatRemovesTheOnlyKnownSceneItemAndTheEventThatMakesAnother()
        {
            Guid a = Guid.Empty;
            Guid b = Guid.Empty;
            Guid mic = Guid.Empty;
            var itemOfA = 0;
            var h = Started(world =>
            {
                a = world.AddScene("A");
                b = world.AddScene("B");
                mic = world.AddInput("Mic", "wasapi_input_capture", 2);
                itemOfA = world.AddItem(a, mic, "Mic");
            });

            h.World.Items[a].Clear();
            h.Event("SceneItemListReindexed", new { sceneName = "A", sceneUuid = a, sceneItems = new[] { new { sceneItemId = 99, sceneItemIndex = 0 } } }, 128);
            h.SettleWithTime();
            Assert.Empty(h.Engine.Snapshot().Scenes.Single(scene => scene.Uuid == a).Items);

            // A scene that the model has not read appeared, and the input was removed: until its scene items are known the model cannot tell what OBS did. Everything is
            // read but the list of inputs that the model asks for then (the check): its answer is on its way.
            EngineEvents.SceneCreated(h, h.World.AddScene("New"), "New");
            EngineEvents.InputRemoved(h, mic, "Mic");
            var itemOfB = h.World.AddItem(b, mic, "Mic");
            ReadTicket? check = null;
            while (h.Take() is { } ticket)
            {
                if (ticket.Requests.Any(request => request.Kind == ReadKind.Inputs))
                {
                    check = ticket;
                    break;
                }

                h.Complete(ticket);
            }

            Assert.NotNull(check);

            // The event that removes the scene item of A arrives (nothing is held for the input any more), then the answer to the check (OBS has the input), and then
            // the event of the scene item that OBS made for it.
            EngineEvents.ItemRemoved(h, a, "A", itemOfA, mic, "Mic");
            h.Complete(check!);
            Assert.Equal(SourceState.Detached, h.Engine.Snapshot().Inputs.Single(input => input.Uuid == mic).State);
            EngineEvents.ItemCreated(h, b, "B", itemOfB, 0, mic, "Mic");
            h.SettleWithTime();

            var snapshot = h.Engine.Snapshot();
            Assert.Equal(SourceState.Detached, snapshot.Inputs.Single(input => input.Uuid == mic).State);
            Assert.Equal(mic, Assert.Single(snapshot.Scenes.Single(scene => scene.Uuid == b).Items).SourceUuid);
            Assert.True(h.Engine.IsSettled);
        }

        // The same for a scene.
        [Fact]
        public void ASceneThatOBSRemovedWhileNotAllSceneItemsWereKnown_IsNotDestroyedByCounting_WhenAReadTookTheOnlyKnownSceneItemAwayAndAnotherOneIsOnItsWay()
        {
            Guid a = Guid.Empty;
            Guid b = Guid.Empty;
            Guid c = Guid.Empty;
            var itemOfA = 0;
            var h = Started(world =>
            {
                a = world.AddScene("A");
                b = world.AddScene("B");
                c = world.AddScene("C");
                itemOfA = world.AddItem(a, c, "C", isScene: true);
            });

            h.World.Items[a].Clear();
            h.Event("SceneItemListReindexed", new { sceneName = "A", sceneUuid = a, sceneItems = new[] { new { sceneItemId = 99, sceneItemIndex = 0 } } }, 128);
            h.SettleWithTime();
            Assert.Empty(h.Engine.Snapshot().Scenes.Single(scene => scene.Uuid == a).Items);

            // A scene that the model has not read appeared, and C was removed (the list of scenes has not been read since): until its scene items are known
            // the model cannot tell what OBS did. OBS made a scene item of B that shows C after the removal.
            EngineEvents.SceneCreated(h, h.World.AddScene("New"), "New");
            h.World.Scenes.RemoveAll(scene => scene.Uuid == c);
            var itemOfB = h.World.AddItem(b, c, "C", isScene: true);
            EngineEvents.SceneRemoved(h, c, "C");
            h.SettleWithTime();
            Assert.Single(h.Engine.Snapshot().DetachedScenes, scene => scene.Uuid == c);

            EngineEvents.ItemRemoved(h, a, "A", itemOfA, c, "C");
            EngineEvents.ItemCreated(h, b, "B", itemOfB, 0, c, "C");
            h.SettleWithTime();

            var snapshot = h.Engine.Snapshot();
            Assert.Single(snapshot.DetachedScenes, scene => scene.Uuid == c);
            Assert.Equal(c, Assert.Single(snapshot.Scenes.Single(scene => scene.Uuid == b).Items).SourceUuid);
            Assert.True(h.Engine.IsSettled);
        }

        // From the random walks (seed 597 of Hostile): a read said that OBS does not have a scene (the list lacks it, and its scene items cannot be read), and
        // its scene item was the only one that the model knew to use an input that OBS had removed and kept alive. Another scene item that uses the input had
        // been made before the scene went, and its event was on its way. The read is about the scene: it says nothing about what the other scenes hold by now,
        // and the model counted, found no scene item, and destroyed the input for good.
        [Fact]
        public void AnInputThatOBSRemoved_IsNotDestroyedByCounting_WhenASceneThatShowedItIsGoneByAReadAndAnotherSceneItemIsOnItsWay()
        {
            Guid doomed = Guid.Empty;
            Guid b = Guid.Empty;
            Guid mic = Guid.Empty;
            var h = Started(world =>
            {
                doomed = world.AddScene("Doomed");
                b = world.AddScene("B");
                mic = world.AddInput("Mic", "wasapi_input_capture", 2);
                world.AddItem(doomed, mic, "Mic");
            });
            EngineEvents.InputRemoved(h, mic, "Mic");
            h.SettleWithTime();
            Assert.Equal(SourceState.Detached, h.Engine.Snapshot().Inputs.Single(input => input.Uuid == mic).State);

            // In OBS a scene item of B uses the input (its event is on its way), and then Doomed is removed, and destroyed. The model reads the list of scenes
            // because a scene appeared: it lacks Doomed, and the scene items of Doomed cannot be read.
            var itemOfB = h.World.AddItem(b, mic, "Mic");
            h.World.Scenes.RemoveAll(scene => scene.Uuid == doomed);
            h.World.Items.Remove(doomed);
            EngineEvents.SceneCreated(h, h.World.AddScene("Other"), "Other");
            h.SettleWithTime();
            Assert.DoesNotContain(h.Engine.Snapshot().Scenes, scene => scene.Uuid == doomed);

            EngineEvents.ItemCreated(h, b, "B", itemOfB, 0, mic, "Mic");
            EngineEvents.SceneRemoved(h, doomed, "Doomed");
            h.SettleWithTime();

            var snapshot = h.Engine.Snapshot();
            Assert.Equal(SourceState.Detached, snapshot.Inputs.Single(input => input.Uuid == mic).State);
            Assert.Equal(mic, Assert.Single(snapshot.Scenes.Single(scene => scene.Uuid == b).Items).SourceUuid);
            Assert.True(h.Engine.IsSettled);
        }

        // The same for a scene that Doomed showed.
        [Fact]
        public void ASceneThatOBSRemoved_IsNotDestroyedByCounting_WhenASceneThatShowedItIsGoneByAReadAndAnotherSceneItemIsOnItsWay()
        {
            Guid doomed = Guid.Empty;
            Guid b = Guid.Empty;
            Guid nested = Guid.Empty;
            var h = Started(world =>
            {
                doomed = world.AddScene("Doomed");
                b = world.AddScene("B");
                nested = world.AddScene("Nested");
                world.AddItem(doomed, nested, "Nested", isScene: true);
            });

            // OBS removed Nested: the list lacks it, and the scene item of Doomed keeps it alive.
            h.World.Scenes.RemoveAll(scene => scene.Uuid == nested);
            EngineEvents.SceneRemoved(h, nested, "Nested");
            h.SettleWithTime();
            Assert.Single(h.Engine.Snapshot().DetachedScenes, scene => scene.Uuid == nested);

            var itemOfB = h.World.AddItem(b, nested, "Nested", isScene: true);
            h.World.Scenes.RemoveAll(scene => scene.Uuid == doomed);
            h.World.Items.Remove(doomed);
            EngineEvents.SceneCreated(h, h.World.AddScene("Other"), "Other");
            h.SettleWithTime();
            Assert.DoesNotContain(h.Engine.Snapshot().Scenes, scene => scene.Uuid == doomed);

            EngineEvents.ItemCreated(h, b, "B", itemOfB, 0, nested, "Nested");
            EngineEvents.SceneRemoved(h, doomed, "Doomed");
            h.SettleWithTime();

            var snapshot = h.Engine.Snapshot();
            Assert.Single(snapshot.DetachedScenes, scene => scene.Uuid == nested);
            Assert.Equal(nested, Assert.Single(snapshot.Scenes.Single(scene => scene.Uuid == b).Items).SourceUuid);
            Assert.True(h.Engine.IsSettled);
        }

        // From a review: a read that says that OBS does not have a scene takes away its scene items, and what OBS did with the sources that they showed
        // is for events that are on their way: the model does not know yet that OBS removed the input, nor that it made another scene item that uses it. A
        // count that says that nothing uses the input, when its removal arrives, must not destroy it: a read says whether OBS still has it.
        [Fact]
        public void ALiveInput_IsNotDestroyedByCounting_WhenASceneThatShowedItIsGoneByAReadAndOBSRemovedItAndUsedItAgainMeanwhile()
        {
            Guid doomed = Guid.Empty;
            Guid b = Guid.Empty;
            Guid mic = Guid.Empty;
            var h = Started(world =>
            {
                doomed = world.AddScene("Doomed");
                b = world.AddScene("B");
                mic = world.AddInput("Mic", "wasapi_input_capture", 2);
                world.AddItem(doomed, mic, "Mic");
            });

            // In OBS a scene item of B uses the input, and Doomed is removed and destroyed (the events are on their way). The model reads the list of scenes because
            // a scene appeared: it lacks Doomed, and the scene items of Doomed cannot be read.
            var itemOfB = h.World.AddItem(b, mic, "Mic");
            h.World.Scenes.RemoveAll(scene => scene.Uuid == doomed);
            h.World.Items.Remove(doomed);
            EngineEvents.SceneCreated(h, h.World.AddScene("Other"), "Other");
            h.SettleWithTime();
            Assert.DoesNotContain(h.Engine.Snapshot().Scenes, scene => scene.Uuid == doomed);
            Assert.Equal(SourceState.Live, h.Engine.Snapshot().Inputs.Single(input => input.Uuid == mic).State);

            // OBS removed the input before it made the scene item of B: the events arrive in that order.
            EngineEvents.InputRemoved(h, mic, "Mic");
            EngineEvents.ItemCreated(h, b, "B", itemOfB, 0, mic, "Mic");
            EngineEvents.SceneRemoved(h, doomed, "Doomed");
            h.SettleWithTime();

            var snapshot = h.Engine.Snapshot();
            Assert.Equal(SourceState.Detached, snapshot.Inputs.Single(input => input.Uuid == mic).State);
            Assert.Equal(mic, Assert.Single(snapshot.Scenes.Single(scene => scene.Uuid == b).Items).SourceUuid);
            Assert.True(h.Engine.IsSettled);
        }

        // The same when another scene item that the model knew used the input: it is taken away by its event, which comes before the removal of the input, and
        // the model is left with none that it knows of.
        [Fact]
        public void ALiveInput_IsNotDestroyedByCounting_WhenASceneThatShowedItIsGoneByAReadAndTheOtherSceneItemThatTheModelKnowsGoesBeforeOBSRemovesIt()
        {
            Guid doomed = Guid.Empty;
            Guid a = Guid.Empty;
            Guid b = Guid.Empty;
            Guid mic = Guid.Empty;
            var itemOfA = 0;
            var h = Started(world =>
            {
                doomed = world.AddScene("Doomed");
                a = world.AddScene("A");
                b = world.AddScene("B");
                mic = world.AddInput("Mic", "wasapi_input_capture", 2);
                world.AddItem(doomed, mic, "Mic");
                itemOfA = world.AddItem(a, mic, "Mic");
            });

            // In OBS the scene item of A goes, a scene item of B uses the input, and Doomed is destroyed. The model reads the list of scenes and finds Doomed gone.
            h.World.Items[a].Clear();
            var itemOfB = h.World.AddItem(b, mic, "Mic");
            h.World.Scenes.RemoveAll(scene => scene.Uuid == doomed);
            h.World.Items.Remove(doomed);
            EngineEvents.SceneCreated(h, h.World.AddScene("Other"), "Other");
            h.SettleWithTime();
            Assert.DoesNotContain(h.Engine.Snapshot().Scenes, scene => scene.Uuid == doomed);

            EngineEvents.ItemRemoved(h, a, "A", itemOfA, mic, "Mic");
            EngineEvents.InputRemoved(h, mic, "Mic");
            EngineEvents.ItemCreated(h, b, "B", itemOfB, 0, mic, "Mic");
            EngineEvents.SceneRemoved(h, doomed, "Doomed");
            h.SettleWithTime();

            var snapshot = h.Engine.Snapshot();
            Assert.Equal(SourceState.Detached, snapshot.Inputs.Single(input => input.Uuid == mic).State);
            Assert.Equal(mic, Assert.Single(snapshot.Scenes.Single(scene => scene.Uuid == b).Items).SourceUuid);
            Assert.True(h.Engine.IsSettled);
        }

        // The same for a scene that Doomed showed.
        [Fact]
        public void ALiveScene_IsNotDestroyedByCounting_WhenASceneThatShowedItIsGoneByAReadAndOBSRemovedItAndUsedItAgainMeanwhile()
        {
            Guid doomed = Guid.Empty;
            Guid b = Guid.Empty;
            Guid nested = Guid.Empty;
            var h = Started(world =>
            {
                doomed = world.AddScene("Doomed");
                b = world.AddScene("B");
                nested = world.AddScene("Nested");
                world.AddItem(doomed, nested, "Nested", isScene: true);
            });

            var itemOfB = h.World.AddItem(b, nested, "Nested", isScene: true);
            h.World.Scenes.RemoveAll(scene => scene.Uuid == doomed || scene.Uuid == nested);
            h.World.Items.Remove(doomed);
            EngineEvents.SceneCreated(h, h.World.AddScene("Other"), "Other");
            h.SettleWithTime();
            Assert.DoesNotContain(h.Engine.Snapshot().Scenes, scene => scene.Uuid == doomed);

            EngineEvents.SceneRemoved(h, nested, "Nested");
            EngineEvents.ItemCreated(h, b, "B", itemOfB, 0, nested, "Nested");
            EngineEvents.SceneRemoved(h, doomed, "Doomed");
            h.SettleWithTime();

            var snapshot = h.Engine.Snapshot();
            Assert.Single(snapshot.DetachedScenes, scene => scene.Uuid == nested);
            Assert.Equal(nested, Assert.Single(snapshot.Scenes.Single(scene => scene.Uuid == b).Items).SourceUuid);
            Assert.True(h.Engine.IsSettled);
        }

        // From a review: a read of the scene items of Doomed found its scene item gone (OBS removed it: the event is on its way, with the events about the
        // input that it showed), and then a read said that OBS does not have Doomed. The scene is destroyed by that read, and what the first read took away
        // is still held: the events lag behind the reads, and only an event that removes the scene lets go of it.
        [Fact]
        public void AnInput_IsNotDestroyedByCounting_WhenAnEmptyReadOfASceneIsFollowedByAReadThatSaysOBSDoesNotHaveIt_AndTheEventsAreOnTheirWay()
        {
            Guid doomed = Guid.Empty;
            Guid b = Guid.Empty;
            Guid mic = Guid.Empty;
            var itemOfDoomed = 0;
            var h = Started(world =>
            {
                doomed = world.AddScene("Doomed");
                b = world.AddScene("B");
                mic = world.AddInput("Mic", "wasapi_input_capture", 2);
                itemOfDoomed = world.AddItem(doomed, mic, "Mic");
            });

            // In OBS the scene item of Doomed goes, and a read of Doomed finds it empty.
            h.World.Items[doomed].Clear();
            h.Event("SceneItemListReindexed", new { sceneName = "Doomed", sceneUuid = doomed, sceneItems = new[] { new { sceneItemId = 99, sceneItemIndex = 0 } } }, 128);
            h.SettleWithTime();
            Assert.Empty(h.Engine.Snapshot().Scenes.Single(scene => scene.Uuid == doomed).Items);

            // Then OBS made a scene item of B that uses the input, and destroyed Doomed. The model reads the list of scenes, which lacks Doomed, and the scene items
            // of Doomed cannot be read.
            var itemOfB = h.World.AddItem(b, mic, "Mic");
            h.World.Scenes.RemoveAll(scene => scene.Uuid == doomed);
            h.World.Items.Remove(doomed);
            EngineEvents.SceneCreated(h, h.World.AddScene("Other"), "Other");
            h.SettleWithTime();
            Assert.DoesNotContain(h.Engine.Snapshot().Scenes, scene => scene.Uuid == doomed);

            // The events arrive late, in the order in which OBS did these things: it removed the input first.
            EngineEvents.InputRemoved(h, mic, "Mic");
            EngineEvents.ItemCreated(h, b, "B", itemOfB, 0, mic, "Mic");
            EngineEvents.ItemRemoved(h, doomed, "Doomed", itemOfDoomed, mic, "Mic");
            EngineEvents.SceneRemoved(h, doomed, "Doomed");
            h.SettleWithTime();

            var snapshot = h.Engine.Snapshot();
            Assert.Equal(SourceState.Detached, snapshot.Inputs.Single(input => input.Uuid == mic).State);
            Assert.Equal(mic, Assert.Single(snapshot.Scenes.Single(scene => scene.Uuid == b).Items).SourceUuid);
            Assert.True(h.Engine.IsSettled);
        }

        // The same for a scene that Doomed showed.
        [Fact]
        public void AScene_IsNotDestroyedByCounting_WhenAnEmptyReadOfASceneIsFollowedByAReadThatSaysOBSDoesNotHaveIt_AndTheEventsAreOnTheirWay()
        {
            Guid doomed = Guid.Empty;
            Guid b = Guid.Empty;
            Guid nested = Guid.Empty;
            var itemOfDoomed = 0;
            var h = Started(world =>
            {
                doomed = world.AddScene("Doomed");
                b = world.AddScene("B");
                nested = world.AddScene("Nested");
                itemOfDoomed = world.AddItem(doomed, nested, "Nested", isScene: true);
            });

            h.World.Items[doomed].Clear();
            h.Event("SceneItemListReindexed", new { sceneName = "Doomed", sceneUuid = doomed, sceneItems = new[] { new { sceneItemId = 99, sceneItemIndex = 0 } } }, 128);
            h.SettleWithTime();
            Assert.Empty(h.Engine.Snapshot().Scenes.Single(scene => scene.Uuid == doomed).Items);

            var itemOfB = h.World.AddItem(b, nested, "Nested", isScene: true);
            h.World.Scenes.RemoveAll(scene => scene.Uuid == doomed || scene.Uuid == nested);
            h.World.Items.Remove(doomed);
            EngineEvents.SceneCreated(h, h.World.AddScene("Other"), "Other");
            h.SettleWithTime();
            Assert.DoesNotContain(h.Engine.Snapshot().Scenes, scene => scene.Uuid == doomed);

            EngineEvents.SceneRemoved(h, nested, "Nested");
            EngineEvents.ItemCreated(h, b, "B", itemOfB, 0, nested, "Nested");
            EngineEvents.ItemRemoved(h, doomed, "Doomed", itemOfDoomed, nested, "Nested");
            EngineEvents.SceneRemoved(h, doomed, "Doomed");
            h.SettleWithTime();

            var snapshot = h.Engine.Snapshot();
            Assert.Single(snapshot.DetachedScenes, scene => scene.Uuid == nested);
            Assert.Equal(nested, Assert.Single(snapshot.Scenes.Single(scene => scene.Uuid == b).Items).SourceUuid);
            Assert.True(h.Engine.IsSettled);
        }
    }
}
