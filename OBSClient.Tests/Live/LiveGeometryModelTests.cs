namespace OBSStudioClient.Tests.Live
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Model;
    using OBSStudioClient.Tests.TestSupport;
    using Xunit.Abstractions;

    /// <summary>
    /// What the model does with the transform of a scene item, its blend mode and the copy of a scene item against a real OBS Studio: it follows the transform of every item when it is asked to
    /// (<see cref="ObsHighVolume.Transforms"/>), what the application sets is what OBS Studio holds when the call returns, a copy is made where it is asked for, and after each step the model has to equal
    /// what OBS Studio holds. The user (the plain client plays him) moves an item and makes its source larger, and the model follows. Everything that is made carries the name prefix of
    /// <see cref="LiveObs.NamePrefix"/> and is removed again.
    /// </summary>
    [Collection("Live OBS")]
    public class LiveGeometryModelTests(ITestOutputHelper output)
    {
        private static readonly TimeSpan Wait = TimeSpan.FromSeconds(20);

        private static object ColorSettings => new { color = 4278190335L, width = 640, height = 360 };

        [LiveUuidChangesFact]
        public async Task TheTransformOfAnItem_IsFollowedSetAndCopied_AsOBSStudioDoesIt_AndTheModelEqualsWhatObsStudioHolds()
        {
            await using var live = await LiveModel.ConnectAsync(output, ObsHighVolume.Transforms);
            var model = live.Model;
            output.WriteLine($"OBS Studio {live.ObsVersionText}, obs-websocket {live.WebSocketVersionText}.");
            await live.AssertModelEqualsObsAsync("at connect");
            var kind = await live.ColorKindAsync();

            var scene = await model.CreateSceneAsync(LiveObs.UniqueName("geo"));
            var other = await model.CreateSceneAsync(LiveObs.UniqueName("geo-other"));
            var inputName = LiveObs.UniqueName("geo-color");
            await scene.CreateInputAsync(inputName, kind, ColorSettings);
            var item = scene.Items[inputName];
            await live.AssertModelEqualsObsAsync("after a scene and an input were made");

            // What OBS Studio gives a new item, as large as its source.
            Assert.NotNull(item.Transform);
            Assert.Equal(640f, item.Transform.SourceWidth);
            Assert.Equal(360f, item.Transform.SourceHeight);
            Assert.Equal(640f, item.Transform.Width);
            Assert.Equal(Bounds.OBS_BOUNDS_NONE, item.Transform.BoundsType);
            output.WriteLine($"a new item: {TransformTexts.Of(item.Transform)}");

            var selected = new List<ObsSceneItem>();
            model.SceneItemSelected += (_, e) =>
            {
                lock (selected)
                {
                    selected.Add(e.Item);
                }
            };

            // Some members, and a crop with them: OBS Studio announces it in two steps, and the call returns when the model shows both.
            await item.SetTransformAsync(new SceneItemTransformUpdate { PositionX = 100f, PositionY = 50f, ScaleX = 0.5f, ScaleY = 0.5f, CropLeft = 20, CropTop = 10 });
            Assert.Equal(100f, item.Transform.PositionX);
            Assert.Equal(50f, item.Transform.PositionY);
            Assert.Equal(0.5f, item.Transform.ScaleX);
            Assert.Equal(320f, item.Transform.Width);
            Assert.Equal(180f, item.Transform.Height);
            Assert.Equal(20, item.Transform.CropLeft);
            Assert.Equal(10, item.Transform.CropTop);
            await live.AssertModelEqualsObsAsync("after the position, the scale and the crop were set");

            await item.SetTransformAsync(new SceneItemTransformUpdate { Rotation = 45f, BoundsType = Bounds.OBS_BOUNDS_STRETCH, BoundsWidth = 200f, BoundsHeight = 100f, BoundsAlignment = 0 });
            Assert.Equal(45f, item.Transform.Rotation);
            Assert.Equal(Bounds.OBS_BOUNDS_STRETCH, item.Transform.BoundsType);
            Assert.Equal(200f, item.Transform.BoundsWidth);
            Assert.Equal(100f, item.Transform.BoundsHeight);
            await live.AssertModelEqualsObsAsync("after the rotation and the bounds were set");

            // The transform that the model shows is what OBS Studio answers when it is asked.
            Assert.Equal(item.Transform, await item.GetTransformAsync());

            // What OBS Studio refuses leaves everything as it is.
            var refused = await Assert.ThrowsAsync<ObsResponseException>(() => item.SetTransformAsync(new SceneItemTransformUpdate { PositionX = 7f, Rotation = 400f }));
            output.WriteLine($"a rotation of 400: {(int)refused.ErrorCode} {refused.ErrorCode}: {refused.ErrorMessage}");
            Assert.Equal(RequestStatusCode.RequestFieldOutOfRange, refused.ErrorCode);
            Assert.Equal(100f, item.Transform.PositionX);
            await live.AssertModelEqualsObsAsync("after a transform that OBS Studio refused");

            // The blend mode is asked for and set; the model does not keep it, and OBS Studio's announcement of the transform that it sends again is no change.
            var before = item.Transform;
            Assert.Equal(BlendMode.OBS_BLEND_NORMAL, await item.GetBlendModeAsync());
            await item.SetBlendModeAsync(BlendMode.OBS_BLEND_SCREEN);
            Assert.Equal(BlendMode.OBS_BLEND_SCREEN, await item.GetBlendModeAsync());
            await Task.Delay(500);
            Assert.Equal(before, item.Transform);
            await live.AssertModelEqualsObsAsync("after the blend mode was set");

            // A copy: on top of the scene, with the transform and the crop of the original, not locked, with the normal blend mode.
            await item.SetLockedAsync(true);
            var copy = await item.DuplicateAsync();
            Assert.Same(scene, copy.Scene);
            Assert.Equal(scene.Items.Count - 1, copy.Index);
            Assert.False(copy.IsLocked);
            Assert.Equal(100f, copy.Transform!.PositionX);
            Assert.Equal(20, copy.Transform.CropLeft);
            Assert.Equal(45f, copy.Transform.Rotation);
            Assert.Equal(BlendMode.OBS_BLEND_NORMAL, await copy.GetBlendModeAsync());
            Assert.Same(item.Source, copy.Source);
            output.WriteLine($"the copy: {TransformTexts.Of(copy.Transform)}");
            await live.AssertModelEqualsObsAsync("after a scene item was copied");

            // A copy in another scene, which the model asks for by the name and the UUID of the scene: OBS Studio reads the destination only when it has the name.
            var elsewhere = await item.DuplicateAsync(other);
            Assert.Same(other, elsewhere.Scene);
            Assert.Single(other.Items);
            Assert.Equal(2, scene.Items.Count);
            await live.AssertModelEqualsObsAsync("after a scene item was copied into another scene");

            // OBS Studio says that each copy was selected, and the model tells it.
            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => { lock (selected) { return selected.Contains(copy) && selected.Contains(elsewhere); } }, Wait), "The copies were not told as selected.");

            // What the user does: moves the item, and makes its source larger.
            await live.Client.SetSceneItemTransform(SceneRef.FromUuid(scene.Uuid), item.Id, new SceneItemTransformUpdate { PositionX = 321f });
            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => item.Transform?.PositionX == 321f, Wait), "The model did not follow the move.");
            await live.AssertModelEqualsObsAsync("after the user moved the scene item");

            // The user makes the source larger while the scene is not rendered: OBS Studio does not announce the size of the items that show it then (it does when the scene is rendered), so the model does not have it
            // yet, and what the application asks for is what OBS Studio holds.
            await live.Client.SetInputSettings(InputRef.FromUuid(item.Source.Uuid), new Dictionary<string, object> { ["width"] = 800, ["height"] = 450 }, true);
            await Task.Delay(1500);
            var larger = await item.GetTransformAsync();
            output.WriteLine($"the source became larger while the scene is not rendered: OBS Studio says {larger.SourceWidth} x {larger.SourceHeight}, the model {item.Transform.SourceWidth} x {item.Transform.SourceHeight}");
            Assert.Equal(800f, larger.SourceWidth);
            Assert.Equal(450f, larger.SourceHeight);
            Assert.Equal(400f, larger.Width);

            // The scene is rendered, and the model has it. So it has it of the other scene when that is rendered.
            await scene.SwitchToAsync();
            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => item.Transform?.SourceWidth == 800f && item.Transform.SourceHeight == 450f, Wait), "The model did not follow the size of the source when the scene was rendered.");
            Assert.Equal(400f, item.Transform.Width);
            await other.SwitchToAsync();
            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => elsewhere.Transform?.SourceWidth == 800f, Wait), "The model did not follow the size of the source when the other scene was rendered.");
            await live.AssertModelEqualsObsAsync("after the source became larger and the scenes were rendered");

            // An item that is removed is gone from the model, and nothing about its transform is left to ask.
            await item.RemoveAsync();
            await live.AssertModelEqualsObsAsync("after the scene item was removed");
        }

        [LiveUuidChangesFact]
        public async Task TheTransformOfAnItem_IsOnlyAskedFor_WhenTheModelIsNotAskedToFollowIt()
        {
            await using var live = await LiveModel.ConnectAsync(output);
            var model = live.Model;
            Assert.Equal(ObsHighVolume.None, model.HighVolume);
            var kind = await live.ColorKindAsync();
            var scene = await model.CreateSceneAsync(LiveObs.UniqueName("geo"));
            var inputName = LiveObs.UniqueName("geo-color");
            await scene.CreateInputAsync(inputName, kind, ColorSettings);
            var item = scene.Items[inputName];
            Assert.Null(item.Transform);

            await item.SetTransformAsync(new SceneItemTransformUpdate { PositionX = 30f, CropRight = 7 });

            Assert.Null(item.Transform);
            var asked = await item.GetTransformAsync();
            Assert.Equal(30f, asked.PositionX);
            Assert.Equal(7, asked.CropRight);

            await live.Client.SetSceneItemTransform(SceneRef.FromUuid(scene.Uuid), item.Id, new SceneItemTransformUpdate { PositionX = 99f });
            await Task.Delay(500);
            Assert.Null(item.Transform);
            Assert.Equal(99f, (await item.GetTransformAsync()).PositionX);
            await live.AssertModelEqualsObsAsync("after the transform was set, and not followed");
        }
    }
}
