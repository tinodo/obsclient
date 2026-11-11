namespace OBSStudioClient.Tests.Simulation
{
    using System.Text.Json;

    /// <summary>
    /// The transform and the blend mode of a scene item, and the copy of one, in the simulation: what OBS Studio 33.0.0 does (measured by LiveGeometryTests) and what obs-websocket 5.7.6 checks and refuses
    /// (read from RequestHandler_SceneItems.cpp): what is announced and in which order, what is refused and how, and what a copy takes from the original.
    /// </summary>
    public class SimGeometryTests
    {
        private static readonly SimQuirks Ideal = SimQuirks.Ideal;

        private static JsonElement Transform(SimHarness h, Guid scene, int item)
        {
            return h.Ok("GetSceneItemTransform", new { sceneUuid = scene.ToString(), sceneItemId = item }).Data.GetProperty("sceneItemTransform");
        }

        private static SimEntry Set(SimHarness h, Guid scene, int item, object transform)
        {
            return h.Call("SetSceneItemTransform", new { sceneUuid = scene.ToString(), sceneItemId = item, sceneItemTransform = transform });
        }

        private static (SimHarness H, Guid Scene, int Item) Start()
        {
            var h = new SimHarness(Ideal);
            var scene = h.CreateScene("A");
            var (_, item) = h.CreateInput(scene, "Backdrop");
            h.Settle();
            h.ClearLog();
            return (h, scene, item);
        }

        [Fact]
        public void TheTransformOfANewItem_IsWhatOBSStudioGivesIt_AndAsLargeAsItsSource()
        {
            var (h, scene, item) = Start();

            var transform = Transform(h, scene, item);

            Assert.Equal(0, transform.GetProperty("positionX").GetDouble());
            Assert.Equal(0, transform.GetProperty("positionY").GetDouble());
            Assert.Equal(1, transform.GetProperty("scaleX").GetDouble());
            Assert.Equal(5, transform.GetProperty("alignment").GetInt32());
            Assert.Equal("OBS_BOUNDS_NONE", transform.GetProperty("boundsType").GetString());
            Assert.Equal(1920, transform.GetProperty("sourceWidth").GetDouble());
            Assert.Equal(1080, transform.GetProperty("sourceHeight").GetDouble());
            Assert.Equal(1920, transform.GetProperty("width").GetDouble());
            Assert.Equal(1080, transform.GetProperty("height").GetDouble());
        }

        [Fact]
        public void ATransformThatIsSet_IsAnnouncedInOneEventForEachStepThatHasMembers()
        {
            var (h, scene, item) = Start();

            Assert.True(Set(h, scene, item, new { positionX = 10.5 }).Succeeded);

            // One event, for the members that are not the crop.
            Assert.Equal(["SceneItemTransformChanged"], h.EventNames());
            Assert.Equal(10.5, h.Events().Single().Data.GetProperty("sceneItemTransform").GetProperty("positionX").GetDouble());

            // A crop together with other members is two events: the first has the crop as it was.
            h.ClearLog();
            Assert.True(Set(h, scene, item, new { positionY = 4, cropLeft = 20 }).Succeeded);
            var events = h.Events("SceneItemTransformChanged");
            Assert.Equal(2, events.Count);
            Assert.Equal(0, events[0].Data.GetProperty("sceneItemTransform").GetProperty("cropLeft").GetInt32());
            Assert.Equal(4, events[0].Data.GetProperty("sceneItemTransform").GetProperty("positionY").GetDouble());
            Assert.Equal(20, events[1].Data.GetProperty("sceneItemTransform").GetProperty("cropLeft").GetInt32());

            // The transform is announced when it is the same as well.
            h.ClearLog();
            Assert.True(Set(h, scene, item, new { positionY = 4 }).Succeeded);
            Assert.Single(h.Events("SceneItemTransformChanged"));
        }

        [Fact]
        public void TheSizeOfAnItem_IsTheSizeOfItsSourceTimesTheScale_WhateverTheBoundsAre()
        {
            var (h, scene, item) = Start();

            Assert.True(Set(h, scene, item, new { scaleX = 0.5, scaleY = 0.25, boundsType = "OBS_BOUNDS_STRETCH", boundsWidth = 640, boundsHeight = 360 }).Succeeded);

            var transform = Transform(h, scene, item);
            Assert.Equal(960, transform.GetProperty("width").GetDouble());
            Assert.Equal(270, transform.GetProperty("height").GetDouble());
            Assert.Equal(640, transform.GetProperty("boundsWidth").GetDouble());
            Assert.Equal("OBS_BOUNDS_STRETCH", transform.GetProperty("boundsType").GetString());
        }

        [Fact]
        public void AChangeOfTheSizeOfTheSource_AnnouncesTheTransformOfTheItemsThatShowIt()
        {
            var (h, scene, item) = Start();
            var input = h.Ok("GetInputList").Data.GetProperty("inputs").EnumerateArray().Single().GetProperty("inputUuid").GetString();

            h.Ok("SetInputSettings", new { inputUuid = input, inputSettings = new { width = 3840, height = 2160 } });

            Assert.Equal(["InputSettingsChanged", "SceneItemTransformChanged"], h.EventNames());
            Assert.Equal(3840, h.Events("SceneItemTransformChanged").Single().Data.GetProperty("sceneItemTransform").GetProperty("width").GetDouble());
            Assert.Equal(3840, Transform(h, scene, item).GetProperty("sourceWidth").GetDouble());
        }

        [Theory]
        [InlineData("{\"rotation\":370}", 402, "The field value of `rotation` is above the maximum of `360.000000`")]
        [InlineData("{\"cropLeft\":-5}", 402, "The field value of `cropLeft` is below the minimum of `0.000000`")]
        [InlineData("{\"cropTop\":100001}", 402, "The field value of `cropTop` is above the maximum of `100000.000000`")]
        [InlineData("{\"boundsWidth\":0}", 402, "The field value of `boundsWidth` is below the minimum of `1.000000`")]
        [InlineData("{\"boundsHeight\":90002}", 402, "The field value of `boundsHeight` is above the maximum of `90001.000000`")]
        [InlineData("{\"positionX\":90002}", 402, "The field value of `positionX` is above the maximum of `90001.000000`")]
        [InlineData("{\"positionY\":-90002}", 402, "The field value of `positionY` is below the minimum of `-90001.000000`")]
        [InlineData("{\"scaleX\":1000}", 402, "The field `scaleX` is too small or large for the current source resolution.")]
        [InlineData("{\"scaleY\":-1000}", 402, "The field `scaleY` is too small or large for the current source resolution.")]
        [InlineData("{\"alignment\":-1}", 402, "The field value of `alignment` is below the minimum of `0.000000`")]
        [InlineData("{\"positionX\":\"left\"}", 401, "The field value of `positionX` must be a number.")]
        [InlineData("{\"cropToBounds\":1}", 401, "The field value of `cropToBounds` must be boolean.")]
        [InlineData("{\"boundsType\":5}", 401, "The field value of `boundsType` must be a string.")]
        [InlineData("{\"boundsType\":\"OBS_BOUNDS_WIDE\"}", 400, "The field `boundsType` has an invalid value.")]
        [InlineData("{\"boundsType\":\"\"}", 403, "The field value of `boundsType` must not be empty.")]
        public void AMemberOfTheTransformThatOBSStudioRefuses_ChangesNothing_AndIsAnnouncedByNothing(string transform, int code, string comment)
        {
            var (h, scene, item) = Start();
            var before = Transform(h, scene, item).GetRawText();

            // A member that is right is checked first: it is not applied either, because obs-websocket checks all of them before it changes any.
            var members = JsonDocument.Parse(transform).RootElement.EnumerateObject().ToDictionary(member => member.Name, member => (object?)member.Value.Clone());
            members.TryAdd("positionX", 77);
            var refusal = Set(h, scene, item, members);

            Assert.Equal((code, comment), (refusal.Code, refusal.Comment));
            Assert.Empty(h.Events());
            Assert.Equal(before, Transform(h, scene, item).GetRawText());
        }

        [Theory]
        [InlineData("{}", 403, "The field value of `sceneItemTransform` must not be empty.")]
        [InlineData("{\"colour\":\"red\"}", 703, "You have not provided any valid transform changes.")]
        [InlineData("{\"positionX\":null}", 703, "You have not provided any valid transform changes.")]
        public void ATransformThatHasNoMemberThatOBSStudioKnows_IsRefused_AndChangesNothing(string transform, int code, string comment)
        {
            var (h, scene, item) = Start();
            var before = Transform(h, scene, item).GetRawText();

            var refusal = Set(h, scene, item, JsonDocument.Parse(transform).RootElement.Clone());

            Assert.Equal((code, comment), (refusal.Code, refusal.Comment));
            Assert.Empty(h.Events());
            Assert.Equal(before, Transform(h, scene, item).GetRawText());
        }

        [Fact]
        public void TheBlendModeOfAnItem_IsNormalToBeginWith_IsSet_AndAnnouncedAsAnUnchangedTransform()
        {
            var (h, scene, item) = Start();
            var data = new { sceneUuid = scene.ToString(), sceneItemId = item };
            Assert.Equal("OBS_BLEND_NORMAL", h.Ok("GetSceneItemBlendMode", data).String("sceneItemBlendMode"));
            var before = Transform(h, scene, item).GetRawText();

            h.Ok("SetSceneItemBlendMode", new { sceneUuid = scene.ToString(), sceneItemId = item, sceneItemBlendMode = "OBS_BLEND_SCREEN" });

            Assert.Equal("OBS_BLEND_SCREEN", h.Ok("GetSceneItemBlendMode", data).String("sceneItemBlendMode"));
            Assert.Equal(["SceneItemTransformChanged"], h.EventNames());
            Assert.Equal(before, h.Events().Single().Data.GetProperty("sceneItemTransform").GetRawText());

            var refusal = h.Call("SetSceneItemBlendMode", new { sceneUuid = scene.ToString(), sceneItemId = item, sceneItemBlendMode = "OBS_BLEND_GLOW" });
            Assert.Equal((400, "The field sceneItemBlendMode has an invalid value."), (refusal.Code, refusal.Comment));
            Assert.Equal("OBS_BLEND_SCREEN", h.Ok("GetSceneItemBlendMode", data).String("sceneItemBlendMode"));
        }

        [Fact]
        public void ACopy_IsOnTopOfItsScene_TakesTheTransformAndTheEnabledStateOfTheOriginal_AndIsNotLocked()
        {
            var (h, scene, item) = Start();
            h.Ok("SetSceneItemTransform", new { sceneUuid = scene.ToString(), sceneItemId = item, sceneItemTransform = new { positionX = 40, cropBottom = 12 } });
            h.Ok("SetSceneItemEnabled", new { sceneUuid = scene.ToString(), sceneItemId = item, sceneItemEnabled = false });
            h.Ok("SetSceneItemLocked", new { sceneUuid = scene.ToString(), sceneItemId = item, sceneItemLocked = true });
            h.Ok("SetSceneItemBlendMode", new { sceneUuid = scene.ToString(), sceneItemId = item, sceneItemBlendMode = "OBS_BLEND_ADDITIVE" });
            h.Settle();
            h.ClearLog();

            var copy = h.Ok("DuplicateSceneItem", new { sceneUuid = scene.ToString(), sceneItemId = item }).Int("sceneItemId");

            Assert.NotEqual(item, copy);
            Assert.Equal(copy, h.Items(scene).Last().Id);
            var transform = Transform(h, scene, copy);
            Assert.Equal(40, transform.GetProperty("positionX").GetDouble());
            Assert.Equal(12, transform.GetProperty("cropBottom").GetInt32());
            var data = new { sceneUuid = scene.ToString(), sceneItemId = copy };
            Assert.False(h.Ok("GetSceneItemEnabled", data).Bool("sceneItemEnabled"));
            Assert.False(h.Ok("GetSceneItemLocked", data).Bool("sceneItemLocked"));
            Assert.Equal("OBS_BLEND_NORMAL", h.Ok("GetSceneItemBlendMode", data).String("sceneItemBlendMode"));

            // The steps in which OBS Studio makes it: the item, its transform without the crop, that it is hidden, its transform with the crop, and that it was selected.
            Assert.Equal(["SceneItemCreated", "SceneItemTransformChanged", "SceneItemEnableStateChanged", "SceneItemTransformChanged", "SceneItemSelected"], h.EventNames());
            var steps = h.Events("SceneItemTransformChanged");
            Assert.Equal(0, steps[0].Data.GetProperty("sceneItemTransform").GetProperty("cropBottom").GetInt32());
            Assert.Equal(12, steps[1].Data.GetProperty("sceneItemTransform").GetProperty("cropBottom").GetInt32());
        }

        [Fact]
        public void TheDestinationOfACopy_IsOnlyReadWhenTheRequestHasItsName_AndAUuidThatIsThereWinsOverTheName()
        {
            var (h, scene, item) = Start();
            var other = h.CreateScene("B");
            var third = h.CreateScene("C");
            h.Settle();

            int Copy(object data) => h.Ok("DuplicateSceneItem", data).Int("sceneItemId");

            // Only the UUID of the destination: OBS Studio does not read it, and the copy is made in the scene of the original.
            var byUuid = Copy(new { sceneUuid = scene.ToString(), sceneItemId = item, destinationSceneUuid = other.ToString() });
            Assert.Contains(h.Items(scene), entry => entry.Id == byUuid);
            Assert.Empty(h.Items(other));

            var byName = Copy(new { sceneUuid = scene.ToString(), sceneItemId = item, destinationSceneName = "B" });
            Assert.Single(h.Items(other));
            Assert.Equal(byName, h.Items(other).Single().Id);

            // The name makes OBS Studio read the destination, and then the UUID wins: the name of another scene does not matter.
            var both = Copy(new { sceneUuid = scene.ToString(), sceneItemId = item, destinationSceneName = "B", destinationSceneUuid = third.ToString() });
            Assert.Single(h.Items(third));
            Assert.Equal(both, h.Items(third).Single().Id);
        }

        [Fact]
        public void ACopyThatCannotBeMade_IsRefusedAndSaysWhy()
        {
            var (h, scene, item) = Start();
            var other = h.CreateScene("B");
            h.Settle();
            var itemOfB = h.CreateSceneItem(scene, other);
            h.Settle();
            h.ClearLog();

            var notThere = h.Call("DuplicateSceneItem", new { sceneUuid = scene.ToString(), sceneItemId = item, destinationSceneName = "Nowhere" });
            Assert.Equal(600, notThere.Code);

            var notAScene = h.Call("DuplicateSceneItem", new { sceneUuid = scene.ToString(), sceneItemId = item, destinationSceneName = "Backdrop" });
            Assert.Equal((602, "The specified source is not a scene."), (notAScene.Code, notAScene.Comment));

            // A scene that is shown in a scene cannot be copied into the scene that it shows (that would make a scene that holds itself): obs_scene_add refuses.
            var loop = h.Call("DuplicateSceneItem", new { sceneUuid = scene.ToString(), sceneItemId = itemOfB, destinationSceneName = "B" });
            Assert.Equal((700, "Failed to create the scene item."), (loop.Code, loop.Comment));

            Assert.Empty(h.Events());
            Assert.Equal(2, h.Items(scene).Count);
        }
    }
}
