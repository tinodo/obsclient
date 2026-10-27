namespace OBSStudioClient.Classes
{
    using OBSStudioClient.Enums;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Describes a change of the transform and crop of a scene item: only the members that are set are sent to OBS Studio,
    /// and OBS Studio leaves every other member as it is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use this to move or scale an item without having to know (and send) the rest of its transform:
    /// <code>await client.SetSceneItemTransform("Scene", itemId, new SceneItemTransformUpdate { PositionX = 100 });</code>
    /// To derive a change from the current state, start from <see cref="SceneItemTransform.ToUpdate"/>:
    /// <code>await client.SetSceneItemTransform("Scene", itemId, current.ToUpdate() with { ScaleX = 2, ScaleY = 2 });</code>
    /// </para>
    /// <para>
    /// A <see cref="SceneItemTransform"/> converts to an update implicitly. The members that OBS Studio calculates
    /// (width, height, source width and source height) are not part of an update. The bounds of a transform without
    /// bounds are left out of it, because OBS Studio refuses a bounds width or height below 1.
    /// </para>
    /// <para>
    /// OBS Studio refuses an update without any member that is set.
    /// </para>
    /// </remarks>
    public sealed record SceneItemTransformUpdate
    {
        /// <summary>Gets the new alignment of the scene item, or <see langword="null"/> to leave it as it is.</summary>
        [JsonPropertyName("alignment")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Alignment { get; init; }

        /// <summary>Gets the new alignment of the bounds, or <see langword="null"/> to leave it as it is.</summary>
        [JsonPropertyName("boundsAlignment")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? BoundsAlignment { get; init; }

        /// <summary>Gets the new height of the bounds (at least 1), or <see langword="null"/> to leave it as it is.</summary>
        [JsonPropertyName("boundsHeight")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public float? BoundsHeight { get; init; }

        /// <summary>Gets the new type of the bounds, or <see langword="null"/> to leave it as it is.</summary>
        [JsonPropertyName("boundsType")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Bounds? BoundsType { get; init; }

        /// <summary>Gets the new width of the bounds (at least 1), or <see langword="null"/> to leave it as it is.</summary>
        [JsonPropertyName("boundsWidth")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public float? BoundsWidth { get; init; }

        /// <summary>Gets the new bottom crop, or <see langword="null"/> to leave it as it is.</summary>
        [JsonPropertyName("cropBottom")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? CropBottom { get; init; }

        /// <summary>Gets the new left crop, or <see langword="null"/> to leave it as it is.</summary>
        [JsonPropertyName("cropLeft")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? CropLeft { get; init; }

        /// <summary>Gets the new right crop, or <see langword="null"/> to leave it as it is.</summary>
        [JsonPropertyName("cropRight")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? CropRight { get; init; }

        /// <summary>Gets the new top crop, or <see langword="null"/> to leave it as it is.</summary>
        [JsonPropertyName("cropTop")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? CropTop { get; init; }

        /// <summary>Gets whether the scene item is cropped to its bounds, or <see langword="null"/> to leave it as it is.</summary>
        [JsonPropertyName("cropToBounds")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? CropToBounds { get; init; }

        /// <summary>Gets the new x position, or <see langword="null"/> to leave it as it is.</summary>
        [JsonPropertyName("positionX")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public float? PositionX { get; init; }

        /// <summary>Gets the new y position, or <see langword="null"/> to leave it as it is.</summary>
        [JsonPropertyName("positionY")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public float? PositionY { get; init; }

        /// <summary>Gets the new rotation in degrees, or <see langword="null"/> to leave it as it is.</summary>
        [JsonPropertyName("rotation")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public float? Rotation { get; init; }

        /// <summary>Gets the new horizontal scale, or <see langword="null"/> to leave it as it is.</summary>
        [JsonPropertyName("scaleX")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public float? ScaleX { get; init; }

        /// <summary>Gets the new vertical scale, or <see langword="null"/> to leave it as it is.</summary>
        [JsonPropertyName("scaleY")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public float? ScaleY { get; init; }

        /// <summary>Gets a value that indicates whether no member is set, which OBS Studio refuses.</summary>
        [JsonIgnore]
        public bool IsEmpty =>
            this.Alignment is null && this.BoundsAlignment is null && this.BoundsHeight is null && this.BoundsType is null
            && this.BoundsWidth is null && this.CropBottom is null && this.CropLeft is null && this.CropRight is null
            && this.CropTop is null && this.CropToBounds is null && this.PositionX is null && this.PositionY is null
            && this.Rotation is null && this.ScaleX is null && this.ScaleY is null;

        /// <summary>
        /// Creates the update that gives a scene item the given transform.
        /// </summary>
        /// <param name="transform">The transform, for example one that was read with GetSceneItemTransform.</param>
        /// <returns>
        /// An update with every member that can be set. The width and height of the bounds are left out when they are
        /// below 1 (a scene item without bounds has none), which is what OBS Studio requires.
        /// </returns>
        public static SceneItemTransformUpdate From(SceneItemTransform transform)
        {
            ArgumentNullException.ThrowIfNull(transform);

            return new SceneItemTransformUpdate
            {
                Alignment = transform.Alignment,
                BoundsAlignment = transform.BoundsAlignment,
                BoundsHeight = transform.BoundsHeight >= 1f ? transform.BoundsHeight : null,
                BoundsType = transform.BoundsType,
                BoundsWidth = transform.BoundsWidth >= 1f ? transform.BoundsWidth : null,
                CropBottom = transform.CropBottom,
                CropLeft = transform.CropLeft,
                CropRight = transform.CropRight,
                CropTop = transform.CropTop,
                CropToBounds = transform.CropToBounds,
                PositionX = transform.PositionX,
                PositionY = transform.PositionY,
                Rotation = transform.Rotation,
                ScaleX = transform.ScaleX,
                ScaleY = transform.ScaleY,
            };
        }

        /// <summary>
        /// Converts a <see cref="SceneItemTransform"/> into the update that gives a scene item that transform.
        /// </summary>
        /// <param name="transform">The transform.</param>
        public static implicit operator SceneItemTransformUpdate(SceneItemTransform transform) => From(transform);
    }
}
