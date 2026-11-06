namespace OBSStudioClient.Tests.Simulation
{
    using System.Text.Json;

    /// <summary>
    /// The behaviour that was measured in a real OBS Studio 33.0.0 with obs-websocket 5.7.6 (the numbered facts of the design of the studio
    /// layer, rows 1 to 26), reproduced by the simulation. A test names its fact. The tests use <see cref="SimQuirks.Ideal"/> unless the quirk
    /// is the point of the test, so that the order of what happens is exact.
    /// </summary>
    public class SimObsTests
    {
        private static readonly SimQuirks Ideal = SimQuirks.Ideal;

        private static string[] MemberNames(SimEntry entry)
        {
            return entry.Data.EnumerateObject().Select(member => member.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        }

        [Fact]
        public void Fact24_ANewSceneIsFirstInTheList_AndSceneListChangedListsItAtOnce()
        {
            var h = new SimHarness(Ideal);
            var original = h.InitialScene;

            var created = h.CreateScene("Second");
            h.Settle();

            var list = h.Ok("GetSceneList");
            var scenes = list.Data.GetProperty("scenes").EnumerateArray().ToArray();
            Assert.Equal(["Second", "Scene"], list.Strings("scenes", "sceneName"));
            Assert.Equal([0, 1], scenes.Select(scene => scene.GetProperty("sceneIndex").GetInt32()).ToArray());
            Assert.Equal(created.ToString(), scenes[0].GetProperty("sceneUuid").GetString());
            Assert.Equal(original.ToString(), scenes[1].GetProperty("sceneUuid").GetString());
            Assert.Equal(original.ToString(), list.String("currentProgramSceneUuid"));

            var changed = h.Events("SceneListChanged").Single();
            Assert.Equal(["Second", "Scene"], changed.Strings("scenes", "sceneName"));
        }

        [Fact]
        public void Fact10_ASceneIsAnnouncedTwice_WithSceneListChangedInBetween()
        {
            var h = new SimHarness(Ideal with { DuplicateSceneCreated = true });

            var uuid = h.CreateScene("Second");
            h.Settle();

            Assert.Equal(["SceneCreated", "SceneListChanged", "SceneCreated"], h.EventNames());
            var created = h.Events("SceneCreated");
            Assert.All(created, entry => Assert.Equal(uuid, entry.Uuid("sceneUuid")));
            Assert.Equal(created[0].Message.Json, created[1].Message.Json);
        }

        [Fact]
        public void Fact10_WithoutTheQuirk_ASceneIsAnnouncedOnce()
        {
            var h = new SimHarness(Ideal with { DuplicateSceneCreated = false });

            h.CreateScene("Second");
            h.Settle();

            Assert.Equal(["SceneCreated", "SceneListChanged"], h.EventNames());
        }

        [Fact]
        public void Fact4_TheListOfScenesLagsBehindTheEvents_ButTheListsOfInputsAndItemsAreRightAtOnce()
        {
            var h = new SimHarness(Ideal with { SceneListLag = SimRange.Exactly(800) });

            var scene = h.CreateScene("Second");
            var (_, item) = h.CreateInput(scene, "Color");
            h.Run(0);

            // The scene was announced, and GetSceneList does not have it yet; the input and the item are there at once.
            Assert.Contains("SceneCreated", h.EventNames());
            Assert.DoesNotContain("Second", h.Ok("GetSceneList").Strings("scenes", "sceneName"));
            Assert.Equal(["Color"], h.InputNames());
            Assert.Equal([(item, 0, "Color")], h.Items(scene));

            h.Run(800);
            Assert.Contains("Second", h.Ok("GetSceneList").Strings("scenes", "sceneName"));

            // And the other way round: the scene is removed, GetSceneList still has it, and the input that it held is gone at once.
            h.Ok("RemoveScene", new { sceneUuid = scene.ToString() });
            h.Run(0);
            Assert.Contains("SceneRemoved", h.EventNames());
            Assert.Contains("Second", h.Ok("GetSceneList").Strings("scenes", "sceneName"));
            Assert.Empty(h.InputNames());
            h.Run(799);
            Assert.Contains("Second", h.Ok("GetSceneList").Strings("scenes", "sceneName"));
            h.Run(1);
            Assert.DoesNotContain("Second", h.Ok("GetSceneList").Strings("scenes", "sceneName"));
        }

        [Fact]
        public void Fact19_ItemIdsAreACounterOfTheScene_NeverUsedAgain_AndANewItemIsOnTop()
        {
            var h = new SimHarness(Ideal);
            var scene = h.CreateScene("S");
            var (a, idA) = h.CreateInput(scene, "A");
            var (_, idB) = h.CreateInput(scene, "B");
            var (_, idC) = h.CreateInput(scene, "C");
            Assert.Equal([1, 2, 3], new[] { idA, idB, idC });

            h.Ok("RemoveSceneItem", new { sceneUuid = scene.ToString(), sceneItemId = idB });
            var idAgain = h.CreateSceneItem(scene, a);

            Assert.Equal(4, idAgain);
            Assert.Equal([(1, 0, "A"), (3, 1, "C"), (4, 2, "A")], h.Items(scene));
            h.Settle();
            var created = h.Events("SceneItemCreated").Last();
            Assert.Equal(4, created.Int("sceneItemId"));
            Assert.Equal(2, created.Int("sceneItemIndex"));
            Assert.Equal(2, h.Ok("GetSceneItemIndex", new { sceneUuid = scene.ToString(), sceneItemId = 4 }).Int("sceneItemIndex"));
        }

        [Fact]
        public void Fact19_ItemIdsAreUniqueInTheirSceneOnly()
        {
            var h = new SimHarness(Ideal);
            var first = h.CreateScene("One");
            var second = h.CreateScene("Two");
            var (_, idInFirst) = h.CreateInput(first, "X");
            var (_, idInSecond) = h.CreateInput(second, "Y");

            Assert.Equal(1, idInFirst);
            Assert.Equal(1, idInSecond);
        }

        [Fact]
        public void Fact20_RemovingAnItemMovesTheOnesAboveItDown_WithoutSceneItemListReindexed()
        {
            var h = new SimHarness(Ideal);
            var scene = h.CreateScene("S");
            h.CreateInput(scene, "A");
            var (_, idB) = h.CreateInput(scene, "B");
            h.CreateInput(scene, "C");
            h.CreateInput(scene, "D");
            h.Settle();
            h.ClearLog();

            h.Ok("RemoveSceneItem", new { sceneUuid = scene.ToString(), sceneItemId = idB });
            h.Settle();

            Assert.Equal([(1, 0, "A"), (3, 1, "C"), (4, 2, "D")], h.Items(scene));
            Assert.Equal(["SceneItemRemoved", "InputRemoved"], h.EventNames());
        }

        [Fact]
        public void Fact21_SetSceneItemIndex_SendsOneSceneItemListReindexedWithEveryItem()
        {
            var h = new SimHarness(Ideal);
            var scene = h.CreateScene("S");
            h.CreateInput(scene, "A");
            h.CreateInput(scene, "B");
            var (_, idC) = h.CreateInput(scene, "C");
            h.Settle();
            h.ClearLog();

            h.Ok("SetSceneItemIndex", new { sceneUuid = scene.ToString(), sceneItemId = idC, sceneItemIndex = 0 });
            h.Settle();

            var reindexed = h.Events().Single();
            Assert.Equal("SceneItemListReindexed", reindexed.Name);
            Assert.Equal(["sceneItems", "sceneName", "sceneUuid"], MemberNames(reindexed));
            var items = reindexed.Data.GetProperty("sceneItems").EnumerateArray().ToArray();
            Assert.Equal([3, 1, 2], items.Select(item => item.GetProperty("sceneItemId").GetInt32()).ToArray());
            Assert.Equal([0, 1, 2], items.Select(item => item.GetProperty("sceneItemIndex").GetInt32()).ToArray());
            Assert.All(items, item => Assert.Equal(["sceneItemId", "sceneItemIndex"], item.EnumerateObject().Select(member => member.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray()));
            Assert.Equal([(3, 0, "C"), (1, 1, "A"), (2, 2, "B")], h.Items(scene));
        }

        [Fact]
        public void Fact21_AnIndexBeyondTheLastItemPutsTheItemOnTop()
        {
            var h = new SimHarness(Ideal);
            var scene = h.CreateScene("S");
            var (_, idA) = h.CreateInput(scene, "A");
            h.CreateInput(scene, "B");

            h.Ok("SetSceneItemIndex", new { sceneUuid = scene.ToString(), sceneItemId = idA, sceneItemIndex = 8000 });

            Assert.Equal([(2, 0, "B"), (1, 1, "A")], h.Items(scene));
        }

        [Fact]
        public void Fact22_TheEventsOfSceneItemsAndRenamesCarryTheDocumentedPayloads()
        {
            var h = new SimHarness(Ideal);
            var scene = h.CreateScene("S");
            var (input, item) = h.CreateInput(scene, "A");
            h.Settle();
            h.ClearLog();

            h.Ok("SetSceneItemEnabled", new { sceneUuid = scene.ToString(), sceneItemId = item, sceneItemEnabled = false });
            h.Ok("SetSceneItemLocked", new { sceneUuid = scene.ToString(), sceneItemId = item, sceneItemLocked = true });
            h.Ok("SetSceneName", new { sceneUuid = scene.ToString(), newSceneName = "S2" });
            h.Ok("SetInputName", new { inputUuid = input.ToString(), newInputName = "A2" });
            h.Ok("RemoveSceneItem", new { sceneUuid = scene.ToString(), sceneItemId = item });
            h.Settle();

            var enabled = h.Events("SceneItemEnableStateChanged").Single();
            Assert.Equal(["sceneItemEnabled", "sceneItemId", "sceneName", "sceneUuid"], MemberNames(enabled));
            Assert.False(enabled.Bool("sceneItemEnabled"));

            var locked = h.Events("SceneItemLockStateChanged").Single();
            Assert.Equal(["sceneItemId", "sceneItemLocked", "sceneName", "sceneUuid"], MemberNames(locked));
            Assert.True(locked.Bool("sceneItemLocked"));

            var sceneRenamed = h.Events("SceneNameChanged").Single();
            Assert.Equal(["oldSceneName", "sceneName", "sceneUuid"], MemberNames(sceneRenamed));
            Assert.Equal("S", sceneRenamed.String("oldSceneName"));
            Assert.Equal("S2", sceneRenamed.String("sceneName"));

            var inputRenamed = h.Events("InputNameChanged").Single();
            Assert.Equal(["inputName", "inputUuid", "oldInputName"], MemberNames(inputRenamed));
            Assert.Equal("A", inputRenamed.String("oldInputName"));

            var removed = h.Events("SceneItemRemoved").Single();
            Assert.Equal(["sceneItemId", "sceneName", "sceneUuid", "sourceName", "sourceUuid"], MemberNames(removed));
            Assert.Equal("S2", removed.String("sceneName"));
            Assert.Equal("A2", removed.String("sourceName"));

            var inputRemoved = h.Events("InputRemoved").Single();
            Assert.Equal(["inputName", "inputUuid"], MemberNames(inputRemoved));
        }

        [Fact]
        public void Fact22_CreatingAnInputAnnouncesItWithItsSettings_ThenItsSceneItem_AndSelectsTheItem()
        {
            var h = new SimHarness(Ideal);
            var scene = h.CreateScene("S");
            h.Settle();
            h.ClearLog();

            var response = h.Ok("CreateInput", new { sceneUuid = scene.ToString(), inputName = "Colour", inputKind = "color_source_v3", inputSettings = new { width = 640, height = 360 } });
            h.Settle();

            Assert.Equal(["InputCreated", "SceneItemCreated", "SceneItemSelected"], h.EventNames());
            var created = h.Events("InputCreated").Single();
            Assert.Equal(["defaultInputSettings", "inputKind", "inputKindCaps", "inputName", "inputSettings", "inputUuid", "unversionedInputKind"], MemberNames(created));
            Assert.Equal("color_source_v3", created.String("inputKind"));
            Assert.Equal("color_source", created.String("unversionedInputKind"));
            Assert.Equal(32777, created.Int("inputKindCaps"));
            Assert.Equal(1920, created.Data.GetProperty("defaultInputSettings").GetProperty("width").GetInt32());
            Assert.Equal(640, created.Data.GetProperty("inputSettings").GetProperty("width").GetInt32());
            Assert.Equal(response.String("inputUuid"), created.String("inputUuid"));

            var item = h.Events("SceneItemCreated").Single();
            Assert.Equal(["sceneItemId", "sceneItemIndex", "sceneName", "sceneUuid", "sourceName", "sourceUuid"], MemberNames(item));
            Assert.Equal(response.Int("sceneItemId"), item.Int("sceneItemId"));
            Assert.Equal(0, item.Int("sceneItemIndex"));
            Assert.Equal(["sceneItemId", "sceneName", "sceneUuid"], MemberNames(h.Events("SceneItemSelected").Single()));
        }

        [Fact]
        public void Fact5_RemoveInput_AnnouncesOnlyInputRemoved_AndTheInputStaysWithItsItemsAndItsName()
        {
            var h = new SimHarness(Ideal);
            var one = h.CreateScene("One");
            var two = h.CreateScene("Two");
            var (x, itemInOne) = h.CreateInput(one, "X");
            var itemInTwo = h.CreateSceneItem(two, x);
            h.Settle();
            h.ClearLog();

            h.Ok("RemoveInput", new { inputUuid = x.ToString() });
            h.Settle();

            Assert.Equal(["InputRemoved"], h.EventNames());
            Assert.Equal([(itemInOne, 0, "X")], h.Items(one));
            Assert.Equal([(itemInTwo, 0, "X")], h.Items(two));
            Assert.Equal(x.ToString(), h.Ok("GetSceneItemSource", new { sceneUuid = two.ToString(), sceneItemId = itemInTwo }).String("sourceUuid"));
            h.Ok("SetSceneItemEnabled", new { sceneUuid = two.ToString(), sceneItemId = itemInTwo, sceneItemEnabled = false });
            Assert.Equal(["X"], h.InputNames());

            var again = h.Call("CreateInput", new { sceneUuid = two.ToString(), inputName = "X", inputKind = "color_source_v3" });
            Assert.Equal(601, again.Code);
        }

        [Fact]
        public void Fact5_TheLastItemOfARemovedInputDestroysIt_AndTheNameIsFreeAgain()
        {
            var h = new SimHarness(Ideal);
            var one = h.CreateScene("One");
            var two = h.CreateScene("Two");
            var (x, itemInOne) = h.CreateInput(one, "X");
            var itemInTwo = h.CreateSceneItem(two, x);
            h.Ok("RemoveInput", new { inputUuid = x.ToString() });
            h.Settle();
            h.ClearLog();

            h.Ok("RemoveSceneItem", new { sceneUuid = one.ToString(), sceneItemId = itemInOne });
            h.Settle();
            Assert.Equal(["SceneItemRemoved"], h.EventNames());
            Assert.Single(h.Ok("GetInputList").Data.GetProperty("inputs").EnumerateArray());

            h.ClearLog();
            h.Ok("RemoveSceneItem", new { sceneUuid = two.ToString(), sceneItemId = itemInTwo });
            h.Settle();
            Assert.Equal(["SceneItemRemoved"], h.EventNames());
            Assert.Empty(h.Ok("GetInputList").Data.GetProperty("inputs").EnumerateArray());
            h.CreateInput(two, "X");
        }

        [Fact]
        public void Fact6_RemovingTheLastItemOfAnInput_DestroysIt_SceneItemRemovedThenInputRemoved_OnceOnly()
        {
            var h = new SimHarness(Ideal);
            var scene = h.CreateScene("S");
            var (x, item) = h.CreateInput(scene, "X");
            h.Settle();
            h.ClearLog();

            h.Ok("RemoveSceneItem", new { sceneUuid = scene.ToString(), sceneItemId = item });
            h.Settle();

            Assert.Equal(["SceneItemRemoved", "InputRemoved"], h.EventNames());
            Assert.Equal(h.Events("SceneItemRemoved").Single().At, h.Events("InputRemoved").Single().At);
            Assert.Equal(600, h.Call("RemoveInput", new { inputUuid = x.ToString() }).Code);
            h.Settle();
            Assert.Single(h.Events("InputRemoved"));
        }

        [Fact]
        public void Fact6_AnInputThatWasRemovedIsNotAnnouncedAgainWhenItsLastItemIsRemoved()
        {
            var h = new SimHarness(Ideal);
            var scene = h.CreateScene("S");
            var (x, item) = h.CreateInput(scene, "X");
            h.Ok("RemoveInput", new { inputUuid = x.ToString() });
            h.Settle();
            h.ClearLog();

            h.Ok("RemoveSceneItem", new { sceneUuid = scene.ToString(), sceneItemId = item });
            h.Settle();

            Assert.Equal(["SceneItemRemoved"], h.EventNames());
            Assert.Equal(600, h.Call("RemoveInput", new { inputUuid = x.ToString() }).Code);
        }

        [Fact]
        public void Fact7_RemovingASceneThatIsNotNested_AnnouncesOnlySceneRemoved_AndDestroysItAtOnce()
        {
            var h = new SimHarness(Ideal);
            var s = h.CreateScene("S");
            var t = h.CreateScene("T");
            var (exclusive, _) = h.CreateInput(s, "Exclusive");
            var (shared, _) = h.CreateInput(s, "Shared");
            var sharedInT = h.CreateSceneItem(t, shared);
            h.Settle();
            h.ClearLog();

            h.Ok("RemoveScene", new { sceneUuid = s.ToString() });
            h.Settle();

            Assert.Equal(["SceneRemoved", "InputRemoved", "SceneListChanged"], h.EventNames());
            Assert.DoesNotContain("SceneItemRemoved", h.EventNames());
            Assert.Equal(exclusive, h.Events("InputRemoved").Single().Uuid("inputUuid"));
            Assert.Equal(600, h.Call("GetSceneItemList", new { sceneUuid = s.ToString() }).Code);
            Assert.Equal(["Shared"], h.InputNames());
            Assert.Equal([(sharedInT, 0, "Shared")], h.Items(t));
        }

        [Fact]
        public void Fact7_TheInputsOfADestroyedScene_AreAnnouncedAboutTwoMillisecondsAfterSceneRemoved()
        {
            var h = new SimHarness(Ideal with { DestroyCascadeDelay = TimeSpan.FromMilliseconds(2) });
            var s = h.CreateScene("S");
            h.CreateScene("T");
            h.CreateInput(s, "One");
            h.CreateInput(s, "Two");
            h.Settle();
            h.ClearLog();

            h.Ok("RemoveScene", new { sceneUuid = s.ToString() });
            h.Settle();

            var removed = h.Events("SceneRemoved").Single();
            var inputs = h.Events("InputRemoved");
            Assert.Equal(2, inputs.Count);
            Assert.All(inputs, input => Assert.True(input.At - removed.At >= TimeSpan.FromMilliseconds(2), $"{input} came {input.At - removed.At} after {removed}"));
            Assert.Equal(["One", "Two"], inputs.Select(input => input.String("inputName")).ToArray());
        }

        [Fact]
        public void Fact7_ASceneThatIsNestedStaysAliveWithoutAnyEvent_UntilItsLastItemIsRemoved()
        {
            var h = new SimHarness(Ideal);
            var p = h.CreateScene("P");
            var b = h.CreateScene("B");
            var s = h.CreateScene("S");
            var leftover = h.CreateSceneItem(p, s);
            var (inner, _) = h.CreateInput(s, "Inner");
            var (shared, _) = h.CreateInput(s, "Shared");
            h.CreateSceneItem(b, shared);
            h.Settle();
            h.ClearLog();

            h.Ok("RemoveScene", new { sceneUuid = s.ToString() });
            h.Run(6000);

            Assert.Equal(["SceneRemoved", "SceneListChanged"], h.EventNames());
            Assert.Equal([(leftover, 0, "S")], h.Items(p));
            Assert.Equal(["Inner", "Shared"], h.Items(s).Select(item => item.Source).ToArray());
            Assert.Equal(2, h.Ok("GetInputList").Data.GetProperty("inputs").GetArrayLength());
            var snapshot = h.Sim.Snapshot();
            Assert.Equal([s], snapshot.DetachedScenes.Select(scene => scene.Uuid).ToArray());
            h.ClearLog();

            h.Ok("RemoveSceneItem", new { sceneUuid = p.ToString(), sceneItemId = leftover });
            h.Settle();

            Assert.Equal(["SceneItemRemoved", "InputRemoved"], h.EventNames());
            Assert.Equal(p, h.Events("SceneItemRemoved").Single().Uuid("sceneUuid"));
            Assert.Equal(s, h.Events("SceneItemRemoved").Single().Uuid("sourceUuid"));
            Assert.Equal(inner, h.Events("InputRemoved").Single().Uuid("inputUuid"));
            Assert.Equal(h.Events("SceneItemRemoved").Single().At, h.Events("InputRemoved").Single().At);
            Assert.Equal(600, h.Call("GetSceneItemList", new { sceneUuid = s.ToString() }).Code);
            Assert.Empty(h.Sim.Snapshot().DetachedScenes);
            Assert.Equal(["Shared"], h.InputNames());
        }

        [Fact]
        public void Fact7_ADetachedSceneAnswersReadsAndKeepsItsItems()
        {
            var h = new SimHarness(Ideal);
            var p = h.CreateScene("P");
            var s = h.CreateScene("S");
            h.CreateSceneItem(p, s);
            var (input, item) = h.CreateInput(s, "Inner");
            h.Ok("RemoveScene", new { sceneUuid = s.ToString() });

            Assert.Equal([(item, 0, "Inner")], h.Items(s));
            Assert.Equal(input.ToString(), h.Ok("GetSceneItemSource", new { sceneUuid = s.ToString(), sceneItemId = item }).String("sourceUuid"));
            Assert.False(h.Ok("GetSceneItemLocked", new { sceneUuid = s.ToString(), sceneItemId = item }).Bool("sceneItemLocked"));

            // A name does not find a scene that was removed.
            Assert.Equal(600, h.Call("GetSceneItemList", new { sceneName = "S" }).Code);
        }

        [Fact]
        public void Fact8_ARemovedScenesNameIsFreeAtOnce_EvenWithALeftoverItem_ButARemovedInputsNameIsNot()
        {
            var h = new SimHarness(Ideal);
            var p = h.CreateScene("P");
            var s = h.CreateScene("S");
            h.CreateSceneItem(p, s);
            var (x, _) = h.CreateInput(p, "X");
            h.Ok("RemoveScene", new { sceneUuid = s.ToString() });
            h.Ok("RemoveInput", new { inputUuid = x.ToString() });

            var again = h.CreateScene("S");

            Assert.NotEqual(s, again);
            Assert.Equal(601, h.Call("CreateInput", new { sceneUuid = p.ToString(), inputName = "X", inputKind = "color_source_v3" }).Code);
        }

        [Fact]
        public void Fact9_SetSceneNameForARemovedSceneThatIsAliveIsNeverAnswered_ButReadsAre()
        {
            var h = new SimHarness(Ideal with { HangOnRemovedSceneRename = true });
            var p = h.CreateScene("P");
            var s = h.CreateScene("S");
            h.CreateSceneItem(p, s);
            h.Ok("RemoveScene", new { sceneUuid = s.ToString() });
            h.Settle();
            h.ClearLog();

            var id = h.Send("SetSceneName", new { sceneUuid = s.ToString(), newSceneName = "Renamed" });
            h.Run(30000);

            Assert.DoesNotContain(h.Log, entry => entry.RequestId == id);
            Assert.Empty(h.Events());
            Assert.True(h.Call("GetSceneItemList", new { sceneUuid = s.ToString() }).Succeeded);
            Assert.Equal("S", h.Sim.Snapshot().DetachedScenes.Single().Name);
        }

        [Fact]
        public void Fact9_WithoutTheQuirk_TheRenameOfARemovedSceneWorks_WithoutAnEvent()
        {
            var h = new SimHarness(Ideal with { HangOnRemovedSceneRename = false });
            var p = h.CreateScene("P");
            var s = h.CreateScene("S");
            h.CreateSceneItem(p, s);
            h.Ok("RemoveScene", new { sceneUuid = s.ToString() });
            h.Settle();
            h.ClearLog();

            h.Ok("SetSceneName", new { sceneUuid = s.ToString(), newSceneName = "Renamed" });
            h.Settle();

            Assert.Empty(h.Events());
            Assert.Equal("Renamed", h.Sim.Snapshot().DetachedScenes.Single().Name);
        }

        [Fact]
        public void Fact13_NamesAreCaseSensitive()
        {
            var h = new SimHarness(Ideal);
            h.CreateScene("Alpha");

            Assert.True(h.Call("CreateScene", new { sceneName = "alpha" }).Succeeded);
            Assert.True(h.Call("CreateScene", new { sceneName = "ALPHA" }).Succeeded);
            Assert.Equal(601, h.Call("CreateScene", new { sceneName = "Alpha" }).Code);
            Assert.Equal(["ALPHA", "alpha", "Alpha", "Scene"], h.Sim.Snapshot().Scenes.Select(scene => scene.Name).ToArray());
            Assert.Equal(600, h.Call("GetSceneItemList", new { sceneName = "aLPHA" }).Code);
        }

        [Fact]
        public void Fact13_ASceneCanTakeTheNameOfAnInput_ButAnInputCannotTakeTheNameOfAScene()
        {
            var h = new SimHarness(Ideal);
            var scene = h.CreateScene("S");
            h.CreateInput(scene, "Same");

            Assert.True(h.Call("CreateScene", new { sceneName = "Same" }).Succeeded);
            Assert.Equal(601, h.Call("CreateInput", new { sceneUuid = scene.ToString(), inputName = "S", inputKind = "color_source_v3" }).Code);
            Assert.Equal(601, h.Call("CreateInput", new { sceneUuid = scene.ToString(), inputName = "Same", inputKind = "color_source_v3" }).Code);
        }

        [Fact]
        public void Fact13_ASceneAndItsSourceShareOneUuid_AndTheSameInputCanBeInASceneSeveralTimes()
        {
            var h = new SimHarness(Ideal);
            var parent = h.CreateScene("Parent");
            var child = h.CreateScene("Child");
            var (input, first) = h.CreateInput(parent, "X");

            var nested = h.CreateSceneItem(parent, child);
            var second = h.CreateSceneItem(parent, input);

            var items = h.Ok("GetSceneItemList", new { sceneUuid = parent.ToString() }).Data.GetProperty("sceneItems").EnumerateArray().ToArray();
            Assert.Equal(3, items.Length);
            Assert.Equal(child.ToString(), items[1].GetProperty("sourceUuid").GetString());
            Assert.Equal("OBS_SOURCE_TYPE_SCENE", items[1].GetProperty("sourceType").GetString());
            Assert.Equal(JsonValueKind.Null, items[1].GetProperty("inputKind").ValueKind);
            Assert.Equal(JsonValueKind.False, items[1].GetProperty("isGroup").ValueKind);
            Assert.Equal("OBS_SOURCE_TYPE_INPUT", items[0].GetProperty("sourceType").GetString());
            Assert.Equal(JsonValueKind.Null, items[0].GetProperty("isGroup").ValueKind);
            Assert.Equal([first, nested, second], items.Select(item => item.GetProperty("sceneItemId").GetInt32()).ToArray());
            Assert.Equal(input.ToString(), items[2].GetProperty("sourceUuid").GetString());
        }

        [Fact]
        public void Fact13_GetSceneItemIdSearchesBySourceName_WithASearchOffset()
        {
            var h = new SimHarness(Ideal);
            var scene = h.CreateScene("S");
            var (x, first) = h.CreateInput(scene, "X");
            h.CreateInput(scene, "Y");
            var second = h.CreateSceneItem(scene, x);
            var third = h.CreateSceneItem(scene, x);

            int Find(object data) => h.Ok("GetSceneItemId", data).Int("sceneItemId");

            Assert.Equal(first, Find(new { sceneName = "S", sourceName = "X" }));
            Assert.Equal(first, Find(new { sceneName = "S", sourceName = "X", searchOffset = 0 }));
            Assert.Equal(second, Find(new { sceneName = "S", sourceName = "X", searchOffset = 1 }));
            Assert.Equal(third, Find(new { sceneName = "S", sourceName = "X", searchOffset = 2 }));
            Assert.Equal(third, Find(new { sceneName = "S", sourceName = "X", searchOffset = -1 }));
            Assert.Equal(600, h.Call("GetSceneItemId", new { sceneName = "S", sourceName = "X", searchOffset = 3 }).Code);
            Assert.Equal(600, h.Call("GetSceneItemId", new { sceneName = "S", sourceName = "Nothing" }).Code);
        }

        [Fact]
        public void Fact18_AnInputWithoutAudioHasNoMuteStateAndNoVolume_AndItsCapsSayso()
        {
            var h = new SimHarness(Ideal);
            var scene = h.CreateScene("S");
            h.CreateInput(scene, "Colour", "color_source_v3");
            h.CreateInput(scene, "Mic", "wasapi_input_capture");

            foreach (var request in new[] { "GetInputMute", "GetInputVolume" })
            {
                var refused = h.Call(request, new { inputName = "Colour" });
                Assert.Equal(604, refused.Code);
                Assert.Equal("The specified input does not support audio.", refused.Comment);
                Assert.True(h.Call(request, new { inputName = "Mic" }).Succeeded);
            }

            Assert.Equal(604, h.Call("SetInputMute", new { inputName = "Colour", inputMuted = true }).Code);
            Assert.Equal(604, h.Call("ToggleInputMute", new { inputName = "Colour" }).Code);
            Assert.Equal(604, h.Call("SetInputVolume", new { inputName = "Colour", inputVolumeMul = 0.5 }).Code);

            var caps = h.Ok("GetInputList").Data.GetProperty("inputs").EnumerateArray()
                .ToDictionary(input => input.GetProperty("inputName").GetString()!, input => input.GetProperty("inputKindCaps").GetInt32());
            Assert.Equal(0, caps["Colour"] & 2);
            Assert.Equal(2, caps["Mic"] & 2);
        }

        [Theory]
        [InlineData("image_source", 32769, false)]
        [InlineData("color_source_v3", 32777, false)]
        [InlineData("browser_source", 32939, true)]
        [InlineData("ffmpeg_source", 8327, true)]
        [InlineData("text_gdiplus_v3", 32777, false)]
        [InlineData("wasapi_input_capture", 130, true)]
        [InlineData("wasapi_output_capture", 642, true)]
        public void Fact18_TheKindsOfInputHaveTheCapsOfARealObsStudio(string kind, int caps, bool audio)
        {
            var h = new SimHarness(Ideal);
            var scene = h.CreateScene("S");
            h.CreateInput(scene, "Input", kind);

            var input = h.Ok("GetInputList").Data.GetProperty("inputs").EnumerateArray().Single();

            Assert.Equal(caps, input.GetProperty("inputKindCaps").GetInt32());
            Assert.Equal(audio, (caps & 2) != 0);
            Assert.Equal(audio, h.Call("GetInputMute", new { inputName = "Input" }).Succeeded);
        }

        [Fact]
        public void AudioChangesAreAnnouncedWhenTheyChange_AndReadBack()
        {
            var h = new SimHarness(Ideal);
            var scene = h.CreateScene("S");
            var (mic, _) = h.CreateInput(scene, "Mic", "wasapi_input_capture");
            h.Settle();
            h.ClearLog();

            h.Ok("SetInputMute", new { inputUuid = mic.ToString(), inputMuted = true });
            h.Ok("SetInputMute", new { inputUuid = mic.ToString(), inputMuted = true });
            Assert.False(h.Ok("ToggleInputMute", new { inputUuid = mic.ToString() }).Bool("inputMuted"));
            h.Ok("SetInputVolume", new { inputUuid = mic.ToString(), inputVolumeDb = -12.5 });
            h.Ok("SetInputVolume", new { inputUuid = mic.ToString(), inputVolumeMul = 0.5 });
            h.Settle();

            Assert.Equal(["InputMuteStateChanged", "InputMuteStateChanged", "InputVolumeChanged", "InputVolumeChanged"], h.EventNames());
            var mutes = h.Events("InputMuteStateChanged");
            Assert.Equal(["inputMuted", "inputName", "inputUuid"], MemberNames(mutes[0]));
            Assert.True(mutes[0].Bool("inputMuted"));
            Assert.False(mutes[1].Bool("inputMuted"));

            var volumes = h.Events("InputVolumeChanged");
            Assert.Equal(["inputName", "inputUuid", "inputVolumeDb", "inputVolumeMul"], MemberNames(volumes[0]));
            Assert.Equal(-12.5, volumes[0].Data.GetProperty("inputVolumeDb").GetDouble(), 3);
            Assert.Equal(0.23713737726211548, volumes[0].Data.GetProperty("inputVolumeMul").GetDouble(), 6);
            var volume = h.Ok("GetInputVolume", new { inputUuid = mic.ToString() });
            Assert.Equal(0.5, volume.Data.GetProperty("inputVolumeMul").GetDouble());
            Assert.Equal(-6.0206, volume.Data.GetProperty("inputVolumeDb").GetDouble(), 3);
        }

        [Fact]
        public void Fact2_TheProgramSceneIsReadAtOnce_ButAnnouncedAtTheEndOfTheTransition()
        {
            var h = new SimHarness(Ideal with { TransitionDuration = TimeSpan.FromMilliseconds(300) });
            var other = h.CreateScene("Other");
            h.Settle();
            h.ClearLog();

            h.Ok("SetCurrentProgramScene", new { sceneUuid = other.ToString() });

            Assert.Equal(other.ToString(), h.Ok("GetCurrentProgramScene").String("sceneUuid"));
            Assert.Equal(other.ToString(), h.Ok("GetSceneList").String("currentProgramSceneUuid"));
            Assert.Equal(["SceneTransitionStarted"], h.EventNames());
            var started = h.Events("SceneTransitionStarted").Single();
            Assert.Equal("Fade", started.String("transitionName"));
            Assert.Equal(["transitionName", "transitionUuid"], MemberNames(started));

            var startedAt = started.At;
            h.Run(299);
            Assert.Equal(["SceneTransitionStarted"], h.EventNames());
            h.Run(2);
            Assert.Equal(["SceneTransitionStarted", "SceneTransitionVideoEnded", "CurrentProgramSceneChanged", "SceneTransitionEnded"], h.EventNames());
            Assert.True(h.Events("CurrentProgramSceneChanged").Single().At - startedAt >= TimeSpan.FromMilliseconds(300));
            Assert.Equal(other, h.Events("CurrentProgramSceneChanged").Single().Uuid("sceneUuid"));
        }

        [Fact]
        public void Fact2_ACutAnnouncesEverythingAtOnce()
        {
            var h = new SimHarness(Ideal);
            var other = h.CreateScene("Other");
            h.Settle();
            h.ClearLog();

            h.Ok("SetCurrentProgramScene", new { sceneName = "Other" });
            h.Settle();

            Assert.Equal(["SceneTransitionStarted", "SceneTransitionVideoEnded", "CurrentProgramSceneChanged", "SceneTransitionEnded"], h.EventNames());
            Assert.Equal("Cut", h.Events("SceneTransitionStarted").Single().String("transitionName"));
            Assert.Equal(other, h.Events("CurrentProgramSceneChanged").Single().Uuid("sceneUuid"));
        }

        [Fact]
        public void Fact25_ASecondSwitchDuringATransitionEndsTheFirst_AndOnlyTheFinalSceneIsAnnounced()
        {
            var h = new SimHarness(Ideal with { TransitionDuration = TimeSpan.FromMilliseconds(300) });
            var p2 = h.CreateScene("P2");
            var p3 = h.CreateScene("P3");
            h.Settle();
            h.ClearLog();

            h.Ok("SetCurrentProgramScene", new { sceneUuid = p2.ToString() });
            h.Run(60);
            var at = h.Now;
            h.Ok("SetCurrentProgramScene", new { sceneUuid = p3.ToString() });
            Assert.Equal(p3.ToString(), h.Ok("GetCurrentProgramScene").String("sceneUuid"));
            h.Settle();

            Assert.Equal(
                [
                    "SceneTransitionStarted",
                    "SceneTransitionEnded",
                    "SceneTransitionStarted",
                    "SceneTransitionVideoEnded",
                    "CurrentProgramSceneChanged",
                    "SceneTransitionEnded",
                ],
                h.EventNames());
            var events = h.Events();
            Assert.Equal(at, events[1].At);
            Assert.Equal(at, events[2].At);
            Assert.Equal(at + TimeSpan.FromMilliseconds(300), events[3].At);
            var changed = h.Events("CurrentProgramSceneChanged").Single();
            Assert.Equal(p3, changed.Uuid("sceneUuid"));
        }

        [Fact]
        public void Fact26_RemovingTheProgramScene_SwitchesAtOnceToTheSceneThatIsFirstInTheList()
        {
            var h = new SimHarness(Ideal with { TransitionDuration = TimeSpan.FromMilliseconds(300) });
            var original = h.InitialScene;
            var p1 = h.CreateScene("P1");
            var p2 = h.CreateScene("P2");
            var p3 = h.CreateScene("P3");
            h.Ok("SetCurrentProgramScene", new { sceneUuid = p3.ToString() });
            h.Run(1000);
            h.ClearLog();

            h.Ok("RemoveScene", new { sceneUuid = p3.ToString() });

            Assert.Equal(p2.ToString(), h.Ok("GetCurrentProgramScene").String("sceneUuid"));
            h.Settle();
            Assert.Equal(["SceneRemoved", "CurrentProgramSceneChanged", "SceneListChanged"], h.EventNames());
            Assert.Equal(p3, h.Events("SceneRemoved").Single().Uuid("sceneUuid"));
            Assert.Equal(p2, h.Events("CurrentProgramSceneChanged").Single().Uuid("sceneUuid"));
            Assert.Empty(h.Events("SceneTransitionStarted"));
            Assert.Equal([p2, p1, original], h.Sim.Snapshot().Scenes.Select(scene => scene.Uuid).ToArray());
        }

        [Fact]
        public void Fact24_AfterRemoveScene_SceneListChangedListsTheRemainingScenes()
        {
            var h = new SimHarness(Ideal);
            var a = h.CreateScene("A");
            h.CreateScene("B");
            h.Settle();
            h.ClearLog();

            h.Ok("RemoveScene", new { sceneUuid = a.ToString() });
            h.Settle();

            var changed = h.Events("SceneListChanged").Single();
            Assert.Equal(["B", "Scene"], changed.Strings("scenes", "sceneName"));
            Assert.Equal(["SceneRemoved", "SceneListChanged"], h.EventNames());
        }

        [Fact]
        public void TheLastSceneCannotBeRemoved()
        {
            var h = new SimHarness(Ideal);

            var refused = h.Call("RemoveScene", new { sceneName = "Scene" });

            Assert.Equal(603, refused.Code);
            Assert.Equal("You cannot remove the last scene in the collection.", refused.Comment);
        }

        [Fact]
        public void ASceneCannotBeAnItemOfItselfOrOfASceneThatIsNestedInIt()
        {
            var h = new SimHarness(Ideal);
            var outer = h.CreateScene("Outer");
            var inner = h.CreateScene("Inner");
            h.CreateSceneItem(outer, inner);

            var itself = h.Call("CreateSceneItem", new { sceneUuid = outer.ToString(), sourceUuid = outer.ToString() });
            var loop = h.Call("CreateSceneItem", new { sceneUuid = inner.ToString(), sourceUuid = outer.ToString() });

            Assert.Equal(703, itself.Code);
            Assert.Equal("You cannot create scene item of a scene within itself.", itself.Comment);
            Assert.Equal(700, loop.Code);
            Assert.Equal("Failed to create the scene item.", loop.Comment);
            h.Sim.CheckInvariants();
        }

        [Fact]
        public void PinnedInputsHaveNoItemsAndStayAlive_UntilTheirPinIsReleased()
        {
            var h = new SimHarness(Ideal);
            var desktop = h.Sim.External.CreateGlobalInput("Desktop Audio", "wasapi_output_capture");
            h.Settle();

            var snapshot = h.Sim.Snapshot();
            Assert.True(snapshot.Inputs.Single().Pinned);
            h.Ok("RemoveInput", new { inputUuid = desktop.ToString() });
            h.Settle();
            Assert.Equal(["InputCreated", "InputRemoved"], h.EventNames());
            Assert.True(h.Sim.Snapshot().Inputs.Single().Removed);

            h.Sim.External.DisableGlobalInput(desktop);
            h.Settle();
            Assert.Empty(h.Sim.Snapshot().Inputs);
            Assert.Single(h.Events("InputRemoved"));
            h.Sim.CheckInvariants();
        }
    }
}
