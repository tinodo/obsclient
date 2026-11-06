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
    }
}
