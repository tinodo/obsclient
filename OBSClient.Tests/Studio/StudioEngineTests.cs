namespace OBSStudioClient.Tests.Studio
{
    using OBSStudioClient.Studio.Engine;

    /// <summary>
    /// The rules of the model of OBS Studio, one at a time: what it does with what OBS Studio sends, and with the answers to its reads. The
    /// tests feed the engine by hand, in the order that is the point of the test. (The random tests are in StudioEngineSimulationTests.)
    /// </summary>
    public class StudioEngineTests
    {
        private static EngineHarness Started(Action<FakeWorld>? setup = null)
        {
            var harness = new EngineHarness();
            harness.World.AddScene("Scene");
            setup?.Invoke(harness.World);
            harness.Start();
            harness.SettleWithTime();
            return harness;
        }

        private static void SceneCreated(EngineHarness h, Guid uuid, string name)
        {
            h.Event("SceneCreated", new { sceneName = name, sceneUuid = uuid, isGroup = false }, 4);
        }

        private static void SceneRemoved(EngineHarness h, Guid uuid, string name)
        {
            h.Event("SceneRemoved", new { sceneName = name, sceneUuid = uuid, isGroup = false }, 4);
        }

        private static void InputCreated(EngineHarness h, Guid uuid, string name, uint caps = 32777)
        {
            h.Event("InputCreated", new { inputName = name, inputUuid = uuid, inputKind = "color_source_v3", unversionedInputKind = "color_source", inputKindCaps = caps, inputSettings = new { }, defaultInputSettings = new { } }, 8);
        }

        private static void InputRemoved(EngineHarness h, Guid uuid, string name)
        {
            h.Event("InputRemoved", new { inputName = name, inputUuid = uuid }, 8);
        }

        private static void ItemCreated(EngineHarness h, Guid scene, string sceneName, int id, int index, Guid source, string sourceName)
        {
            h.Event("SceneItemCreated", new { sceneName, sceneUuid = scene, sourceName, sourceUuid = source, sceneItemId = id, sceneItemIndex = index }, 128);
        }

        private static void ItemRemoved(EngineHarness h, Guid scene, string sceneName, int id, Guid source, string sourceName)
        {
            h.Event("SceneItemRemoved", new { sceneName, sceneUuid = scene, sourceName, sourceUuid = source, sceneItemId = id }, 128);
        }

        private static EngineScene SceneNamed(EngineSnapshot snapshot, string name)
        {
            return snapshot.Scenes.Single(scene => scene.Name == name);
        }

        [Fact]
        public void TheFirstRead_BuildsTheModel()
        {
            Guid gaming = Guid.Empty;
            Guid mic = Guid.Empty;
            var harness = Started(world =>
            {
                gaming = world.AddScene("Gaming");
                world.Program = gaming;
                mic = world.AddInput("Mic", "wasapi_input_capture", 2);
                world.AddItem(gaming, mic, "Mic");
            });

            var snapshot = harness.Engine.Snapshot();
            Assert.Equal(StudioEngineState.Synchronized, snapshot.State);
            Assert.Equal(0, snapshot.Incomplete);
            Assert.Equal(new[] { "Gaming", "Scene" }, snapshot.Scenes.Select(scene => scene.Name).ToArray());
            Assert.Equal(new[] { 0, 1 }, snapshot.Scenes.Select(scene => scene.Index).ToArray());
            Assert.Equal(gaming, snapshot.ProgramSceneUuid);
            var item = Assert.Single(SceneNamed(snapshot, "Gaming").Items);
            Assert.Equal((1, mic, 0, true, false), (item.Id, item.SourceUuid, item.Index, item.Enabled, item.Locked));
            var input = Assert.Single(snapshot.Inputs);
            Assert.True(input.HasAudio);
            Assert.False(input.Muted);
        }

        [Fact]
        public void ASceneThatIsAnnouncedTwice_IsOneScene()
        {
            var harness = Started();
            var uuid = harness.World.AddScene("Gaming");

            SceneCreated(harness, uuid, "Gaming");
            harness.Event("SceneListChanged", new { scenes = harness.World.Scenes.Select((s, i) => new { sceneIndex = i, sceneName = s.Name, sceneUuid = s.Uuid }).ToArray() }, 4);
            SceneCreated(harness, uuid, "Gaming");
            harness.SettleWithTime();

            Assert.Equal(new[] { "Gaming", "Scene" }, harness.Engine.Snapshot().Scenes.Select(scene => scene.Name).ToArray());
        }

        [Fact]
        public void ASceneThatAnEventAnnouncedAndTheListDoesNotHaveYet_IsKept_UntilTheListHasIt()
        {
            var harness = Started();
            var uuid = harness.World.AddScene("Gaming");
            var created = harness.Now;
            var seen = new List<bool>();

            (int, object?) Lagging(ReadRequest request)
            {
                if (request.Kind == ReadKind.Scenes)
                {
                    seen.Add(harness.Engine.Snapshot().Scenes.Any(scene => scene.Name == "Gaming"));
                    if (harness.Now - created < TimeSpan.FromSeconds(1.5))
                    {
                        // The list does not have the scene that was just created.
                        var without = harness.World.Scenes.Where(s => s.Uuid != uuid).Select((s, i) => new { sceneIndex = i, sceneName = s.Name, sceneUuid = s.Uuid }).ToArray();
                        var program = harness.World.Scenes.First(s => s.Uuid != uuid);
                        return (100, new { currentProgramSceneName = program.Name, currentProgramSceneUuid = program.Uuid, currentPreviewSceneName = (string?)null, currentPreviewSceneUuid = (Guid?)null, scenes = without });
                    }
                }

                return harness.World.Answer(request);
            }

            SceneCreated(harness, uuid, "Gaming");
            harness.SettleWithTime(Lagging);

            Assert.True(seen.Count >= 3, "The list was not read again while it lagged.");
            Assert.All(seen, visible => Assert.True(visible, "The scene was removed while the list lagged."));
            Assert.Contains(harness.Engine.Snapshot().Scenes, scene => scene.Name == "Gaming");
            Assert.DoesNotContain(harness.Engine.TakeChanges(), change => change.Kind == StudioChangeKind.Repaired);
        }

        [Fact]
        public void ASceneThatTheListNeverHas_IsRemovedAfterTheGrace_AndItIsReported()
        {
            var harness = Started();
            SceneCreated(harness, Guid.NewGuid(), "Phantom");

            harness.SettleWithTime();

            Assert.DoesNotContain(harness.Engine.Snapshot().Scenes, scene => scene.Name == "Phantom");
            Assert.Contains(harness.Engine.TakeChanges(), change => change.Kind == StudioChangeKind.Repaired && change.Text == "Phantom");
            Assert.True(harness.Now >= TimeSpan.FromSeconds(10), "The scene was removed before the grace was over.");
        }

        [Fact]
        public void AnEvent_BeatsARead_ThatWasSentBeforeIt()
        {
            Guid gaming = Guid.Empty;
            Guid cam = Guid.Empty;
            var harness = Started(world =>
            {
                gaming = world.AddScene("Gaming");
                cam = world.AddInput("Cam");
                world.AddItem(gaming, cam, "Cam");
            });

            // An item is created, which makes the scene be read.
            var extra = harness.World.AddInput("Extra");
            var extraItem = harness.World.AddItem(gaming, extra, "Extra");
            InputCreated(harness, extra, "Extra");
            ItemCreated(harness, gaming, "Gaming", extraItem, 1, extra, "Extra");
            var read = harness.Take();
            Assert.NotNull(read);

            // The item is disabled after the read was sent, and the read (which OBS ran before that) says that it is enabled.
            harness.Event("SceneItemEnableStateChanged", new { sceneName = "Gaming", sceneUuid = gaming, sceneItemId = 1, sceneItemEnabled = false }, 128);
            harness.Complete(read!);

            var items = SceneNamed(harness.Engine.Snapshot(), "Gaming").Items;
            Assert.False(items.Single(item => item.Id == 1).Enabled);
        }

        [Fact]
        public void ARead_ThatWasSentAfterAnEvent_IsNewerThanTheEvent()
        {
            Guid gaming = Guid.Empty;
            var harness = Started(world =>
            {
                gaming = world.AddScene("Gaming");
                var cam = world.AddInput("Cam");
                world.AddItem(gaming, cam, "Cam");
            });
            harness.Event("SceneItemEnableStateChanged", new { sceneName = "Gaming", sceneUuid = gaming, sceneItemId = 1, sceneItemEnabled = false }, 128);

            // OBS enabled it again, and the event of that has not arrived; a read that was sent after the first event says so.
            harness.World.Items[gaming][0] = harness.World.Items[gaming][0] with { Enabled = true };
            harness.Event("SceneItemLockStateChanged", new { sceneName = "Gaming", sceneUuid = gaming, sceneItemId = 99, sceneItemLocked = true }, 128);
            harness.SettleWithTime();

            Assert.True(SceneNamed(harness.Engine.Snapshot(), "Gaming").Items.Single().Enabled);
        }

        [Fact]
        public void AnInputThatOBSRemoves_WhileSceneItemsUseIt_IsDetached_AndItsItemsStay_UntilTheLastItemGoes()
        {
            Guid a = Guid.Empty, b = Guid.Empty, x = Guid.Empty;
            var harness = Started(world =>
            {
                a = world.AddScene("A");
                b = world.AddScene("B");
                x = world.AddInput("X");
                world.AddItem(a, x, "X");
                world.AddItem(b, x, "X");
            });

            InputRemoved(harness, x, "X");
            var detached = harness.Engine.Snapshot();
            Assert.Equal(SourceState.Detached, Assert.Single(detached.Inputs).State);
            Assert.Single(SceneNamed(detached, "A").Items);
            Assert.Single(SceneNamed(detached, "B").Items);

            ItemRemoved(harness, a, "A", 1, x, "X");
            Assert.Equal(SourceState.Detached, Assert.Single(harness.Engine.Snapshot().Inputs).State);

            // OBS destroys it when the last scene item goes, and says nothing more about the input.
            ItemRemoved(harness, b, "B", 1, x, "X");
            var gone = harness.Engine.Snapshot();
            Assert.Empty(gone.Inputs);
            Assert.Empty(SceneNamed(gone, "A").Items);
            Assert.Empty(SceneNamed(gone, "B").Items);
            Assert.Contains(harness.Engine.TakeChanges(), change => change.Kind == StudioChangeKind.SourceDetached);
        }

        [Fact]
        public void ALiveInput_ThatLosesItsLastItem_IsRemovedByTheEventOfOBS()
        {
            Guid a = Guid.Empty, x = Guid.Empty;
            var harness = Started(world =>
            {
                a = world.AddScene("A");
                x = world.AddInput("X");
                world.AddItem(a, x, "X");
            });

            ItemRemoved(harness, a, "A", 1, x, "X");
            Assert.Single(harness.Engine.Snapshot().Inputs);

            InputRemoved(harness, x, "X");
            Assert.Empty(harness.Engine.Snapshot().Inputs);
        }

        [Fact]
        public void ARemovedScene_ThatIsNotNested_Goes_AndItsInputsWaitForTheirOwnEvents()
        {
            Guid s = Guid.Empty, t = Guid.Empty, i1 = Guid.Empty, i2 = Guid.Empty;
            var harness = Started(world =>
            {
                s = world.AddScene("S");
                t = world.AddScene("T");
                i1 = world.AddInput("I1");
                i2 = world.AddInput("I2");
                world.AddItem(s, i1, "I1");
                world.AddItem(s, i2, "I2");
                world.AddItem(t, i2, "I2");
            });

            SceneRemoved(harness, s, "S");
            var snapshot = harness.Engine.Snapshot();
            Assert.DoesNotContain(snapshot.Scenes, scene => scene.Name == "S");
            Assert.Empty(snapshot.DetachedScenes);
            Assert.Equal(2, snapshot.Inputs.Count);

            // OBS destroys the input that only the scene used, and says so.
            InputRemoved(harness, i1, "I1");
            Assert.Equal(new[] { i2 }, harness.Engine.Snapshot().Inputs.Select(input => input.Uuid).ToArray());
            Assert.Single(SceneNamed(harness.Engine.Snapshot(), "T").Items);
        }

        [Fact]
        public void ARemovedScene_ThatIsNested_IsDetached_WithItsItems_UntilTheItemInTheParentGoes()
        {
            Guid parent = Guid.Empty, child = Guid.Empty, w = Guid.Empty;
            var harness = Started(world =>
            {
                parent = world.AddScene("Parent");
                child = world.AddScene("Child");
                w = world.AddInput("W");
                world.AddItem(child, w, "W");
                world.AddItem(parent, child, "Child", isScene: true);
            });

            SceneRemoved(harness, child, "Child");
            var detached = harness.Engine.Snapshot();
            Assert.DoesNotContain(detached.Scenes, scene => scene.Name == "Child");
            var ghost = Assert.Single(detached.DetachedScenes);
            Assert.Equal(child, ghost.Uuid);
            Assert.Single(ghost.Items);
            Assert.Single(SceneNamed(detached, "Parent").Items);
            Assert.Single(detached.Inputs);

            // The item in the parent goes: OBS destroys the scene, and the input that only it used, and says so for the input.
            ItemRemoved(harness, parent, "Parent", 1, child, "Child");
            var afterwards = harness.Engine.Snapshot();
            Assert.Empty(afterwards.DetachedScenes);
            InputRemoved(harness, w, "W");
            Assert.Empty(harness.Engine.Snapshot().Inputs);
        }

        [Fact]
        public void RemovingAnItem_MovesTheIndexesOfTheItemsAboveIt_WithoutAnEvent()
        {
            Guid s = Guid.Empty;
            Guid[] inputs = new Guid[3];
            var harness = Started(world =>
            {
                s = world.AddScene("S");
                for (var i = 0; i < 3; i++)
                {
                    inputs[i] = world.AddInput($"I{i}");
                    world.AddItem(s, inputs[i], $"I{i}");
                }
            });

            ItemRemoved(harness, s, "S", 2, inputs[1], "I1");

            var items = SceneNamed(harness.Engine.Snapshot(), "S").Items;
            Assert.Equal(new[] { (1, 0), (3, 1) }, items.Select(item => (item.Id, item.Index)).ToArray());
        }

        [Fact]
        public void AReindexEvent_GivesTheIndexesOfAllItsItems()
        {
            Guid s = Guid.Empty;
            var harness = Started(world =>
            {
                s = world.AddScene("S");
                for (var i = 0; i < 3; i++)
                {
                    world.AddItem(s, world.AddInput($"I{i}"), $"I{i}");
                }
            });

            harness.Event("SceneItemListReindexed", new { sceneName = "S", sceneUuid = s, sceneItems = new[] { new { sceneItemId = 3, sceneItemIndex = 0 }, new { sceneItemId = 1, sceneItemIndex = 1 }, new { sceneItemId = 2, sceneItemIndex = 2 } } }, 128);

            var items = SceneNamed(harness.Engine.Snapshot(), "S").Items;
            Assert.Equal(new[] { (3, 0), (1, 1), (2, 2) }, items.Select(item => (item.Id, item.Index)).ToArray());
        }

        [Fact]
        public void ARead_ThatWasSentBeforeTheRemovalOfAnItem_DoesNotBringItBack()
        {
            Guid s = Guid.Empty, x = Guid.Empty, y = Guid.Empty;
            var harness = Started(world =>
            {
                s = world.AddScene("S");
                x = world.AddInput("X");
                y = world.AddInput("Y");
                world.AddItem(s, x, "X");
            });

            // A new item makes the scene be read; the item that was there is removed after the read was sent, and the read still lists it.
            harness.World.AddItem(s, y, "Y");
            InputCreated(harness, y, "Y");
            ItemCreated(harness, s, "S", 2, 1, y, "Y");
            var read = harness.Take();
            ItemRemoved(harness, s, "S", 1, x, "X");
            harness.Complete(read!);

            var items = SceneNamed(harness.Engine.Snapshot(), "S").Items;
            Assert.Equal(new[] { 2 }, items.Select(item => item.Id).ToArray());
            Assert.Equal(0, items[0].Index);
        }

        [Fact]
        public void TheProgramScene_IsReadAfterItsEvent_AndNeverTakenFromIt()
        {
            Guid a = Guid.Empty, b = Guid.Empty;
            var harness = Started(world =>
            {
                a = world.AddScene("A");
                b = world.AddScene("B");
                world.Program = a;
            });

            harness.World.Program = b;
            harness.Event("CurrentProgramSceneChanged", new { sceneName = "B", sceneUuid = b }, 4);
            Assert.Equal(a, harness.Engine.Snapshot().ProgramSceneUuid);

            harness.SettleWithTime();
            Assert.Equal(b, harness.Engine.Snapshot().ProgramSceneUuid);
        }

        [Fact]
        public void AnInputThatOBSRemovedBeforeTheConnection_IsFoundByTheListCheck_WhenItsLastItemGoes()
        {
            Guid a = Guid.Empty, x = Guid.Empty;
            var harness = Started(world =>
            {
                a = world.AddScene("A");
                x = world.AddInput("X");
                world.AddItem(a, x, "X");
            });

            // The input was removed (with its item kept alive) before the connection: it looks like any other input. Its last item goes, and
            // OBS destroys it without an event.
            harness.World.Inputs.RemoveAll(input => input.Uuid == x);
            harness.World.Items[a].Clear();
            ItemRemoved(harness, a, "A", 1, x, "X");
            Assert.Single(harness.Engine.Snapshot().Inputs);

            harness.SettleWithTime();
            Assert.Empty(harness.Engine.Snapshot().Inputs);
        }

        [Fact]
        public void ARead_ThatIsNotReady_IsRepeated()
        {
            var harness = new EngineHarness();
            harness.World.AddScene("Scene");
            harness.Start();
            var failed = false;

            (int Code, object? Data) NotReadyOnce(ReadRequest request)
            {
                if (!failed && request.Kind == ReadKind.Scenes)
                {
                    failed = true;
                    return (207, null);
                }

                return harness.World.Answer(request);
            }

            harness.SettleWithTime(NotReadyOnce);

            Assert.True(failed);
            Assert.Equal(StudioEngineState.Synchronized, harness.Engine.State);
            Assert.Single(harness.Engine.Snapshot().Scenes);
        }

        [Fact]
        public void ALateCopyOfTheCreationOfAScene_DoesNotBringBackAScene_ThatWasRemoved()
        {
            var harness = Started();
            var uuid = harness.World.AddScene("Gaming");
            SceneCreated(harness, uuid, "Gaming");
            harness.SettleWithTime();

            harness.World.Scenes.RemoveAll(scene => scene.Uuid == uuid);
            harness.World.Items.Remove(uuid);
            SceneRemoved(harness, uuid, "Gaming");
            Assert.DoesNotContain(harness.Engine.Snapshot().Scenes, scene => scene.Name == "Gaming");

            // The second copy of the event of its creation arrives only now.
            SceneCreated(harness, uuid, "Gaming");
            Assert.DoesNotContain(harness.Engine.Snapshot().Scenes, scene => scene.Name == "Gaming");
            harness.SettleWithTime();
            Assert.DoesNotContain(harness.Engine.Snapshot().Scenes, scene => scene.Name == "Gaming");
        }

        [Fact]
        public void AListThatStillHasARemovedScene_DoesNotBringItBack()
        {
            var harness = Started();
            var uuid = harness.World.AddScene("Gaming");
            SceneCreated(harness, uuid, "Gaming");
            harness.SettleWithTime();
            var gaming = harness.World.Scenes.First(scene => scene.Uuid == uuid);

            harness.World.Scenes.RemoveAll(scene => scene.Uuid == uuid);
            harness.World.Items.Remove(uuid);
            SceneRemoved(harness, uuid, "Gaming");

            // Another scene is created, which makes the list be read. The list lags: it still has the scene that was removed.
            var other = harness.World.AddScene("Other");
            SceneCreated(harness, other, "Other");

            (int, object?) Lagging(ReadRequest request)
            {
                if (request.Kind == ReadKind.Scenes)
                {
                    var scenes = harness.World.Scenes.Append(gaming).Select((s, i) => new { sceneIndex = i, sceneName = s.Name, sceneUuid = s.Uuid }).ToArray();
                    var program = harness.World.Scenes.First(s => s.Uuid == harness.World.Program);
                    return (100, new { currentProgramSceneName = program.Name, currentProgramSceneUuid = program.Uuid, currentPreviewSceneName = (string?)null, currentPreviewSceneUuid = (Guid?)null, scenes });
                }

                return harness.World.Answer(request);
            }

            harness.SettleWithTime(Lagging);

            var names = harness.Engine.Snapshot().Scenes.Select(scene => scene.Name).ToArray();
            Assert.DoesNotContain("Gaming", names);
            Assert.Contains("Other", names);
        }

        [Fact]
        public void TheChanges_TellWhatHappened_InTheOrderItHappened()
        {
            var harness = Started();
            harness.Engine.TakeChanges();
            var uuid = harness.World.AddScene("Gaming");
            var input = harness.World.AddInput("Mic", "wasapi_input_capture", 2);
            var item = harness.World.AddItem(uuid, input, "Mic");

            SceneCreated(harness, uuid, "Gaming");
            InputCreated(harness, input, "Mic", caps: 2);
            ItemCreated(harness, uuid, "Gaming", item, 0, input, "Mic");
            harness.SettleWithTime();

            var kinds = harness.Engine.TakeChanges().Select(change => change.Kind).ToArray();
            Assert.Equal(StudioChangeKind.SceneAdded, kinds[0]);
            Assert.Contains(StudioChangeKind.InputAdded, kinds);
            Assert.Contains(StudioChangeKind.ItemAdded, kinds);
            Assert.True(Array.IndexOf(kinds, StudioChangeKind.InputAdded) > Array.IndexOf(kinds, StudioChangeKind.SceneAdded));
        }
    }
}
