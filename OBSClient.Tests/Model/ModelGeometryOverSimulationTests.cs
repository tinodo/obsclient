namespace OBSStudioClient.Tests.Model
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Model;
    using OBSStudioClient.Tests.Simulation;
    using OBSStudioClient.Tests.TestSupport;

    /// <summary>
    /// The transform and the blend mode of a scene item, and the copy of one, over the whole stack: an <see cref="ObsModel"/> on a real <see cref="ObsClient"/>, connected to the simulation of OBS Studio
    /// (<see cref="SimObsServer"/>), with the lags and the order of what OBS Studio really does. What someone else does to the transform is followed, and what the application asks for is shown by the model
    /// when the call returns; in both cases what the model shows is what the simulation holds (<see cref="ModelMirror"/>, which compares the transforms when the model follows them).
    /// </summary>
    public class ModelGeometryOverSimulationTests
    {
        private const string Colour = "color_source_v3";
        private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

        private static SimQuirks Quirks(string preset)
        {
            return preset switch
            {
                "Ideal" => ModelSimRig.DefaultQuirks,
                "Real" => SimQuirks.Real,
                "Hostile" => SimQuirks.Hostile,
                _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, null),
            };
        }

        // Two scenes, "Gaming" with two color sources (a webcam and a backdrop, 1920 by 1080 as long as nobody changes it) and an empty "Chatting". The model is connected first, as in the other tests of the
        // changes: OBS Studio lists what was made a moment ago late (Real and Hostile).
        private static async Task<ModelSimRig> StartAsync(SimQuirks? quirks = null, bool follow = true, int seed = 7)
        {
            var rig = new ModelSimRig(quirks, new ObsModelOptions { HighVolume = follow ? ObsHighVolume.Transforms : ObsHighVolume.None }, seed);
            try
            {
                await rig.ConnectAsync().WithTimeoutAsync(Patience);
                await rig.ConvergedAsync("the first read", Patience);
                rig.World(world =>
                {
                    var gaming = world.CreateScene("Gaming");
                    world.CreateInput(gaming, "Webcam", Colour);
                    world.CreateInput(gaming, "Backdrop", Colour);
                    world.CreateScene("Chatting");
                });
                await rig.ConvergedAsync("the scenes and the inputs", Patience);
                return rig;
            }
            catch
            {
                await rig.DisposeAsync();
                throw;
            }
        }

        [Theory]
        [InlineData("Ideal")]
        [InlineData("Real")]
        [InlineData("Hostile")]
        public async Task WhatSomeoneElseDoesToTheTransformOfAnItem_IsFollowedByTheModel(string preset)
        {
            await using var rig = await StartAsync(Quirks(preset));
            var webcam = rig.Model.Scenes["Gaming"].Items["Webcam"];
            var scene = webcam.Scene.Uuid;
            var id = webcam.Id;

            // The transform that a new item has, from the read of the list of its scene.
            Assert.NotNull(webcam.Transform);
            Assert.Equal(0f, webcam.Transform.PositionX);
            Assert.Equal(1f, webcam.Transform.ScaleX);
            Assert.Equal(1920f, webcam.Transform.Width);

            rig.World(world => world.SetItemTransform(scene, id, new SimTransformUpdate { PositionX = 12.5f, PositionY = -3f, ScaleX = 0.5f, ScaleY = 0.25f, CropLeft = 10, CropTop = 5 }));
            await rig.ConvergedAsync("a scene item that was moved, scaled and cropped", Patience);
            Assert.Equal(12.5f, webcam.Transform.PositionX);
            Assert.Equal(-3f, webcam.Transform.PositionY);
            Assert.Equal(960f, webcam.Transform.Width);
            Assert.Equal(270f, webcam.Transform.Height);
            Assert.Equal(10, webcam.Transform.CropLeft);
            Assert.Equal(5, webcam.Transform.CropTop);
            Assert.Contains(rig.Changes, change => change.Kind == ObsChangeKind.PropertyChanged && change.PropertyName == nameof(ObsSceneItem.Transform) && ReferenceEquals(change.Entity, webcam));

            rig.World(world => world.SetItemTransform(scene, id, new SimTransformUpdate { Rotation = 90f, BoundsType = "OBS_BOUNDS_STRETCH", BoundsWidth = 640f, BoundsHeight = 360f }));
            await rig.ConvergedAsync("a scene item that was rotated and given bounds", Patience);
            Assert.Equal(90f, webcam.Transform.Rotation);
            Assert.Equal(Bounds.OBS_BOUNDS_STRETCH, webcam.Transform.BoundsType);
            Assert.Equal(640f, webcam.Transform.BoundsWidth);

            // The source of the item becomes larger: OBS Studio announces the transform of the item, which is as large as the source times its scale.
            var backdrop = rig.Model.Inputs["Webcam"];
            rig.World(world => world.SetSettings(backdrop.Uuid, new System.Text.Json.Nodes.JsonObject { ["width"] = 3840, ["height"] = 2160 }));
            await rig.ConvergedAsync("a source that became larger", Patience);
            Assert.Equal(1920f, webcam.Transform.Width);
            Assert.Equal(3840f, webcam.Transform.SourceWidth);
        }

        [Theory]
        [InlineData("Ideal")]
        [InlineData("Real")]
        [InlineData("Hostile")]
        public async Task ASceneItemThatIsMadeWhileTheModelFollowsTheTransforms_HasItsTransform_AsSoonAsTheModelTellsOfIt(string preset)
        {
            await using var rig = await StartAsync(Quirks(preset));
            var told = new List<ObsSceneItem>();
            rig.Model.Changed += (_, e) =>
            {
                if (e.Kind == ObsChangeKind.Added && e.Entity is ObsSceneItem item)
                {
                    lock (told)
                    {
                        told.Add(item);
                    }
                }
            };

            var chatting = rig.Model.Scenes["Chatting"].Uuid;
            rig.World(world => world.CreateInput(chatting, "Overlay", Colour));
            await rig.ConvergedAsync("a scene item that is new", Patience);

            ObsSceneItem[] added;
            lock (told)
            {
                added = told.ToArray();
            }

            var overlay = Assert.Single(added);
            Assert.NotNull(overlay.Transform);
            Assert.Equal(1920f, overlay.Transform.Width);
        }

        [Theory]
        [InlineData("Ideal")]
        [InlineData("Real")]
        [InlineData("Hostile")]
        public async Task SetTransformAsync_IsShownByTheModel_WhenTheCallReturns(string preset)
        {
            await using var rig = await StartAsync(Quirks(preset));
            var webcam = rig.Model.Scenes["Gaming"].Items["Webcam"];

            // The members that are left out stay as they are, and a crop comes together with the other members as two events in OBS Studio: the call waits for both.
            await webcam.SetTransformAsync(new SceneItemTransformUpdate { PositionX = 100f, PositionY = 50f, ScaleX = 2f, CropLeft = 20, CropTop = 10 }).WithTimeoutAsync(Patience);
            Assert.Equal(100f, webcam.Transform!.PositionX);
            Assert.Equal(50f, webcam.Transform.PositionY);
            Assert.Equal(2f, webcam.Transform.ScaleX);
            Assert.Equal(1f, webcam.Transform.ScaleY);
            Assert.Equal(20, webcam.Transform.CropLeft);
            Assert.Equal(10, webcam.Transform.CropTop);
            await rig.ConvergedAsync("a transform that was set", Patience);

            // The transform that the model shows, changed in one member.
            await webcam.SetTransformAsync(webcam.Transform.ToUpdate() with { Rotation = 45f }).WithTimeoutAsync(Patience);
            Assert.Equal(45f, webcam.Transform.Rotation);
            Assert.Equal(100f, webcam.Transform.PositionX);
            await rig.ConvergedAsync("a transform that was changed in one member", Patience);

            var asked = await webcam.GetTransformAsync().WithTimeoutAsync(Patience);
            Assert.Equal(webcam.Transform, asked);
        }

        [Fact]
        public async Task SetTransformAsync_ReturnsWhenOBSStudioHasAnswered_WhenTheModelDoesNotFollowTheTransforms()
        {
            await using var rig = await StartAsync(follow: false);
            var webcam = rig.Model.Scenes["Gaming"].Items["Webcam"];
            Assert.Null(webcam.Transform);

            await webcam.SetTransformAsync(new SceneItemTransformUpdate { PositionX = 30f, CropRight = 7 }).WithTimeoutAsync(Patience);

            Assert.Null(webcam.Transform);
            var asked = await webcam.GetTransformAsync().WithTimeoutAsync(Patience);
            Assert.Equal(30f, asked.PositionX);
            Assert.Equal(7, asked.CropRight);
            await rig.ConvergedAsync("a transform that the model does not follow", Patience);
        }

        [Theory]
        [InlineData("Ideal")]
        [InlineData("Real")]
        [InlineData("Hostile")]
        public async Task TheBlendModeOfAnItem_IsAskedForAndSet_AndTheTransformStaysAsItIs(string preset)
        {
            await using var rig = await StartAsync(Quirks(preset));
            var webcam = rig.Model.Scenes["Gaming"].Items["Webcam"];
            var before = webcam.Transform;

            Assert.Equal(BlendMode.OBS_BLEND_NORMAL, await webcam.GetBlendModeAsync().WithTimeoutAsync(Patience));
            await webcam.SetBlendModeAsync(BlendMode.OBS_BLEND_MULTIPLY).WithTimeoutAsync(Patience);
            Assert.Equal(BlendMode.OBS_BLEND_MULTIPLY, await webcam.GetBlendModeAsync().WithTimeoutAsync(Patience));

            // OBS Studio sends the transform again when the blend mode changes, and it is the same: the model tells of no change.
            Assert.Equal(before, webcam.Transform);
            await rig.ConvergedAsync("a blend mode that was set", Patience);
            Assert.DoesNotContain(rig.Changes, change => change.Kind == ObsChangeKind.PropertyChanged && change.PropertyName == nameof(ObsSceneItem.Transform));
        }

        [Theory]
        [InlineData("Ideal")]
        [InlineData("Real")]
        [InlineData("Hostile")]
        public async Task ACopyOfAnItem_IsOnTopOfItsScene_AndHasTheTransformOfTheOriginal(string preset)
        {
            await using var rig = await StartAsync(Quirks(preset));
            var gaming = rig.Model.Scenes["Gaming"];
            var webcam = gaming.Items["Webcam"];
            await webcam.SetTransformAsync(new SceneItemTransformUpdate { PositionX = 40f, CropBottom = 12 }).WithTimeoutAsync(Patience);
            await webcam.SetLockedAsync(true).WithTimeoutAsync(Patience);
            var selected = new List<ObsSceneItem>();
            rig.Model.SceneItemSelected += (_, e) =>
            {
                lock (selected)
                {
                    selected.Add(e.Item);
                }
            };

            var copy = await webcam.DuplicateAsync().WithTimeoutAsync(Patience);

            Assert.NotSame(webcam, copy);
            Assert.Same(gaming, copy.Scene);
            Assert.Same(webcam.Source, copy.Source);
            Assert.Equal(gaming.Items.Count - 1, copy.Index);
            Assert.Equal(40f, copy.Transform!.PositionX);
            Assert.Equal(12, copy.Transform.CropBottom);
            Assert.False(copy.IsLocked);
            await rig.ConvergedAsync("a scene item that was copied", Patience);
            await ModelRig.EventuallyAsync(() => { lock (selected) { return selected.Count > 0; } }, "the copy to be told as selected");
            lock (selected)
            {
                Assert.Same(copy, Assert.Single(selected));
            }
        }

        [Theory]
        [InlineData("Ideal")]
        [InlineData("Real")]
        [InlineData("Hostile")]
        public async Task ACopyOfAnItem_CanBeMadeInAnotherScene_ByItsName(string preset)
        {
            await using var rig = await StartAsync(Quirks(preset));
            var webcam = rig.Model.Scenes["Gaming"].Items["Webcam"];
            var chatting = rig.Model.Scenes["Chatting"];

            var copy = await webcam.DuplicateAsync(chatting).WithTimeoutAsync(Patience);

            Assert.Same(chatting, copy.Scene);
            Assert.Single(chatting.Items);
            Assert.Equal(2, rig.Model.Scenes["Gaming"].Items.Count);
            await rig.ConvergedAsync("a scene item that was copied into another scene", Patience);
        }
    }
}
