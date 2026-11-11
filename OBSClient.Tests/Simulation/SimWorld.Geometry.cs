namespace OBSStudioClient.Tests.Simulation
{
    using System.Text.Json.Nodes;
    using static OBSStudioClient.Tests.Simulation.SimJson;

    /// <summary>
    /// A change of the transform of a scene item (<c>SetSceneItemTransform</c>): the members that are set. The numbers are single precision, as OBS Studio keeps them.
    /// </summary>
    internal sealed class SimTransformUpdate
    {
        /// <summary>Gets or sets the x position.</summary>
        public float? PositionX { get; set; }

        /// <summary>Gets or sets the y position.</summary>
        public float? PositionY { get; set; }

        /// <summary>Gets or sets the rotation, in degrees.</summary>
        public float? Rotation { get; set; }

        /// <summary>Gets or sets the horizontal scale.</summary>
        public float? ScaleX { get; set; }

        /// <summary>Gets or sets the vertical scale.</summary>
        public float? ScaleY { get; set; }

        /// <summary>Gets or sets the alignment.</summary>
        public int? Alignment { get; set; }

        /// <summary>Gets or sets the alignment of the bounds.</summary>
        public int? BoundsAlignment { get; set; }

        /// <summary>Gets or sets the type of the bounds.</summary>
        public string? BoundsType { get; set; }

        /// <summary>Gets or sets the width of the bounds.</summary>
        public float? BoundsWidth { get; set; }

        /// <summary>Gets or sets the height of the bounds.</summary>
        public float? BoundsHeight { get; set; }

        /// <summary>Gets or sets whether the item is cropped to its bounds.</summary>
        public bool? CropToBounds { get; set; }

        /// <summary>Gets or sets the crop of the left.</summary>
        public int? CropLeft { get; set; }

        /// <summary>Gets or sets the crop of the right.</summary>
        public int? CropRight { get; set; }

        /// <summary>Gets or sets the crop of the top.</summary>
        public int? CropTop { get; set; }

        /// <summary>Gets or sets the crop of the bottom.</summary>
        public int? CropBottom { get; set; }

        /// <summary>Gets a value indicating whether the change sets a member of the transform without the crop: the ones that obs-websocket gives to libobs with <c>obs_sceneitem_set_info2</c>.</summary>
        public bool HasInfo => PositionX is not null || PositionY is not null || Rotation is not null || ScaleX is not null || ScaleY is not null || Alignment is not null || BoundsAlignment is not null
            || BoundsType is not null || BoundsWidth is not null || BoundsHeight is not null || CropToBounds is not null;

        /// <summary>Gets a value indicating whether the change sets a crop, which obs-websocket gives to libobs with <c>obs_sceneitem_set_crop</c>.</summary>
        public bool HasCrop => CropLeft is not null || CropRight is not null || CropTop is not null || CropBottom is not null;
    }

    /// <summary>What a simulated OBS Studio does with the transform and the blend mode of a scene item, and with the copy of a scene item.</summary>
    internal sealed partial class SimWorld
    {
        /// <summary>The blend modes that obs-websocket knows.</summary>
        public static readonly string[] BlendModes =
        [
            "OBS_BLEND_NORMAL", "OBS_BLEND_ADDITIVE", "OBS_BLEND_SUBTRACT", "OBS_BLEND_SCREEN", "OBS_BLEND_MULTIPLY", "OBS_BLEND_LIGHTEN", "OBS_BLEND_DARKEN",
        ];

        /// <summary>The types of bounds that obs-websocket knows.</summary>
        public static readonly string[] BoundsTypes =
        [
            "OBS_BOUNDS_NONE", "OBS_BOUNDS_STRETCH", "OBS_BOUNDS_SCALE_INNER", "OBS_BOUNDS_SCALE_OUTER", "OBS_BOUNDS_SCALE_TO_WIDTH", "OBS_BOUNDS_SCALE_TO_HEIGHT", "OBS_BOUNDS_MAX_ONLY",
        ];

        /// <summary>
        /// Changes the transform of a scene item (SetSceneItemTransform). obs-websocket gives the members of the transform to libobs in two steps, and each step announces the transform:
        /// the position, rotation, scale, alignment, bounds and whether the item is cropped to them first, and the crop after that (measured with OBS Studio 33.0.0: a change of the crop together with
        /// the other members was two events, the first with the crop as it was). An event comes for every step that has members, also when nothing changes (a change of the position to the position that
        /// it has was an event; LiveGeometryTests).
        /// </summary>
        public void SetItemTransform(SimItem item, SimTransformUpdate update)
        {
            var transform = item.Transform;
            if (update.HasInfo)
            {
                transform.PositionX = update.PositionX ?? transform.PositionX;
                transform.PositionY = update.PositionY ?? transform.PositionY;
                transform.Rotation = update.Rotation ?? transform.Rotation;
                transform.ScaleX = update.ScaleX ?? transform.ScaleX;
                transform.ScaleY = update.ScaleY ?? transform.ScaleY;
                transform.Alignment = update.Alignment ?? transform.Alignment;
                transform.BoundsAlignment = update.BoundsAlignment ?? transform.BoundsAlignment;
                transform.BoundsType = update.BoundsType ?? transform.BoundsType;
                transform.BoundsWidth = update.BoundsWidth ?? transform.BoundsWidth;
                transform.BoundsHeight = update.BoundsHeight ?? transform.BoundsHeight;
                transform.CropToBounds = update.CropToBounds ?? transform.CropToBounds;
                EmitTransform(item);
            }

            if (update.HasCrop)
            {
                transform.CropLeft = update.CropLeft ?? transform.CropLeft;
                transform.CropRight = update.CropRight ?? transform.CropRight;
                transform.CropTop = update.CropTop ?? transform.CropTop;
                transform.CropBottom = update.CropBottom ?? transform.CropBottom;
                EmitTransform(item);
            }
        }

        /// <summary>Sets the blend mode of a scene item (SetSceneItemBlendMode). OBS Studio sends the transform of the item again, which is the same (measured with OBS Studio 33.0.0, LiveGeometryTests).</summary>
        public void SetItemBlendMode(SimItem item, string mode)
        {
            item.BlendMode = mode;
            EmitTransform(item);
        }

        /// <summary>
        /// Makes a copy of a scene item, on top of the scene that it is made in (DuplicateSceneItem). It is announced in the steps that were measured with OBS Studio 33.0.0 (LiveGeometryTests): the scene item,
        /// its transform without the crop, that it is hidden when the original is, its transform with the crop, and that it was selected. The copy has the transform and whether the original is shown, but
        /// it is not locked and has the normal blend mode.
        /// </summary>
        public SimItem DuplicateItem(SimItem original, SimScene destination)
        {
            // ASSUMPTION: a source that is a scene is refused for a scene that it shows, directly or through other scenes, as when it is added (obs_scene_add fails). Not measured.
            if (original.Source is SimScene nested && (ReferenceEquals(nested, destination) || Reaches(nested, destination)))
            {
                throw new SimFailure(SimStatus.ResourceCreationFailed, "Failed to create the scene item.");
            }

            var copy = new SimItem(destination.NextItemId++, destination, original.Source) { Index = destination.Items.Count };
            destination.Items.Add(copy);
            original.Source.References++;
            copy.Transform = original.Transform.Clone();
            var crop = (copy.Transform.CropLeft, copy.Transform.CropRight, copy.Transform.CropTop, copy.Transform.CropBottom);
            copy.Transform.CropLeft = copy.Transform.CropRight = copy.Transform.CropTop = copy.Transform.CropBottom = 0;
            if (destination.IsLive)
            {
                Emit("SceneItemCreated", Obj(
                    ("sceneItemId", copy.Id),
                    ("sceneItemIndex", copy.Index),
                    ("sceneName", destination.Name),
                    ("sceneUuid", destination.UuidText),
                    ("sourceName", original.Source.Name),
                    ("sourceUuid", original.Source.UuidText)));
                EmitTransform(copy);
            }

            if (!original.Enabled)
            {
                copy.Enabled = false;
                if (destination.IsLive)
                {
                    Emit("SceneItemEnableStateChanged", Obj(
                        ("sceneItemEnabled", false),
                        ("sceneItemId", copy.Id),
                        ("sceneName", destination.Name),
                        ("sceneUuid", destination.UuidText)));
                }
            }

            (copy.Transform.CropLeft, copy.Transform.CropRight, copy.Transform.CropTop, copy.Transform.CropBottom) = crop;
            if (destination.IsLive)
            {
                EmitTransform(copy);
                Emit("SceneItemSelected", Obj(("sceneItemId", copy.Id), ("sceneName", destination.Name), ("sceneUuid", destination.UuidText)));
            }

            UpdateActivity();
            return copy;
        }

        // The size that a source has changes the size of the items that show it, and OBS Studio announces the transform of each of them (measured with OBS Studio 33.0.0, LiveGeometryTests).
        private void AnnounceResizedSource(SimInput input, (double Width, double Height) before)
        {
            if (SourceSize(input) == before)
            {
                return;
            }

            foreach (var scene in _liveScenes)
            {
                foreach (var item in scene.Items.Where(item => ReferenceEquals(item.Source, input)))
                {
                    EmitTransform(item);
                }
            }
        }

        // The transform of an item, as the event says it. A scene that was removed announces nothing (fact 7).
        private void EmitTransform(SimItem item)
        {
            if (item.Scene.IsLive)
            {
                Emit("SceneItemTransformChanged", Obj(
                    ("sceneItemId", item.Id),
                    ("sceneItemTransform", TransformJson(item)),
                    ("sceneName", item.Scene.Name),
                    ("sceneUuid", item.Scene.UuidText)));
            }
        }
    }
}
