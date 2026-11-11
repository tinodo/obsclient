namespace OBSStudioClient.Classes
{
    using OBSStudioClient.Enums;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides a class for SceneItemTransform. 
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="SceneItemTransform"/> class.
    /// </remarks>
    /// <param name="alignment">The alignment.</param>
    /// <param name="boundsAlignment">The bounds alignment.</param>
    /// <param name="boundsHeight">The bounds height.</param>
    /// <param name="boundsType">The type of bounds.</param>
    /// <param name="boundsWidth">The width of the bounds.</param>
    /// <param name="cropBottom">The bottom crop.</param>
    /// <param name="cropLeft">The left crop.</param>
    /// <param name="cropRight">The right crop.</param>
    /// <param name="cropTop">The top crop.</param>
    /// <param name="height">The height.</param>
    /// <param name="positionX">The x position.</param>
    /// <param name="positionY">The y position.</param>
    /// <param name="rotation">The rotation.</param>
    /// <param name="scaleX">The horizontal scale.</param>
    /// <param name="scaleY">The vertical scale.</param>
    /// <param name="sourceHeight">The source height.</param>
    /// <param name="sourceWidth">The source width.</param>
    /// <param name="width">The width.</param>
    [method: JsonConstructor]
    public sealed class SceneItemTransform(int alignment, int boundsAlignment, float boundsHeight, Bounds boundsType, float boundsWidth, int cropBottom, int cropLeft, int cropRight, int cropTop, float height, float positionX, float positionY, float rotation, float scaleX, float scaleY, float sourceHeight, float sourceWidth, float width) : IEquatable<SceneItemTransform>
    {
        /// <summary>
        /// Gets the alignment of the scene transform.
        /// </summary>
        [JsonPropertyName("alignment")]
        public int Alignment { get; } = alignment;

        /// <summary>
        /// Gets the alignment of the bounds of the scene transform.
        /// </summary>
        [JsonPropertyName("boundsAlignment")]
        public int BoundsAlignment { get; } = boundsAlignment;

        /// <summary>
        /// Gets the height of the bounds of the scene transform.
        /// </summary>
        [JsonPropertyName("boundsHeight")]
        public float BoundsHeight { get; } = boundsHeight;

        /// <summary>
        /// Gets the coordinated for the bounds type.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("boundsType")]
        public Bounds BoundsType { get; } = boundsType;

        /// <summary>
        /// Gets the width of the scene transform bounds.
        /// </summary>
        [JsonPropertyName("boundsWidth")]
        public float BoundsWidth { get; } = boundsWidth;

        /// <summary>
        /// Gets the bottom crop.
        /// </summary>
        [JsonPropertyName("cropBottom")]
        public int CropBottom { get; } = cropBottom;

        /// <summary>
        /// Gets the left crop.
        /// </summary>
        [JsonPropertyName("cropLeft")]
        public int CropLeft { get; } = cropLeft;

        /// <summary>
        /// Gets the right crop.
        /// </summary>
        [JsonPropertyName("cropRight")]
        public int CropRight { get; } = cropRight;

        /// <summary>
        /// Gets the top crop.
        /// </summary>
        [JsonPropertyName("cropTop")]
        public int CropTop { get; } = cropTop;

        /// <summary>
        /// Gets the height.
        /// </summary>
        [JsonPropertyName("height")]
        public float Height { get; } = height;

        /// <summary>
        /// Gets the x position.
        /// </summary>
        [JsonPropertyName("positionX")]
        public float PositionX { get; } = positionX;

        /// <summary>
        /// Gets the y position.
        /// </summary>
        [JsonPropertyName("positionY")]
        public float PositionY { get; } = positionY;

        /// <summary>
        /// Gets the rotation.
        /// </summary>
        [JsonPropertyName("rotation")]
        public float Rotation { get; } = rotation;

        /// <summary>
        /// Gets the horizontal scale.
        /// </summary>
        [JsonPropertyName("scaleX")]
        public float ScaleX { get; } = scaleX;

        /// <summary>
        /// Gets the vertical scale.
        /// </summary>
        [JsonPropertyName("scaleY")]
        public float ScaleY { get; } = scaleY;

        /// <summary>
        /// Gets the source height.
        /// </summary>
        [JsonPropertyName("sourceHeight")]
        public float SourceHeight { get; } = sourceHeight;

        /// <summary>
        /// Gets the source width.
        /// </summary>
        [JsonPropertyName("sourceWidth")]
        public float SourceWidth { get; } = sourceWidth;

        /// <summary>
        /// Gets the width.
        /// </summary>
        [JsonPropertyName("width")]
        public float Width { get; } = width;

        /// <summary>
        /// Gets a value that indicates whether the scene item is cropped to its bounds.
        /// </summary>
        /// <remarks>
        /// <see langword="null"/> when OBS Studio does not send it. When this transform is sent to OBS Studio, a
        /// <see langword="null"/> value is ignored. It is an <see langword="init"/> property, rather than a
        /// constructor parameter, to keep the public constructor of this class unchanged.
        /// </remarks>
        [JsonPropertyName("cropToBounds")]
        public bool? CropToBounds { get; init; }

        /// <summary>
        /// Creates the <see cref="SceneItemTransformUpdate"/> that gives a scene item this transform, ready to be changed
        /// with a <see langword="with"/> expression.
        /// </summary>
        /// <returns>The update. The bounds of a transform without bounds are left out of it, because OBS Studio refuses a bounds width or height below 1.</returns>
        public SceneItemTransformUpdate ToUpdate() => SceneItemTransformUpdate.From(this);

        /// <summary>
        /// Gets whether another transform has the same value in every member.
        /// </summary>
        /// <param name="other">The other transform.</param>
        /// <returns>True when every member is the same.</returns>
        public bool Equals(SceneItemTransform? other)
        {
            return other is not null
                && this.Alignment == other.Alignment
                && this.BoundsAlignment == other.BoundsAlignment
                && this.BoundsHeight.Equals(other.BoundsHeight)
                && this.BoundsType == other.BoundsType
                && this.BoundsWidth.Equals(other.BoundsWidth)
                && this.CropBottom == other.CropBottom
                && this.CropLeft == other.CropLeft
                && this.CropRight == other.CropRight
                && this.CropTop == other.CropTop
                && this.CropToBounds == other.CropToBounds
                && this.Height.Equals(other.Height)
                && this.PositionX.Equals(other.PositionX)
                && this.PositionY.Equals(other.PositionY)
                && this.Rotation.Equals(other.Rotation)
                && this.ScaleX.Equals(other.ScaleX)
                && this.ScaleY.Equals(other.ScaleY)
                && this.SourceHeight.Equals(other.SourceHeight)
                && this.SourceWidth.Equals(other.SourceWidth)
                && this.Width.Equals(other.Width);
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj) => this.Equals(obj as SceneItemTransform);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(this.Alignment);
            hash.Add(this.BoundsAlignment);
            hash.Add(this.BoundsHeight);
            hash.Add(this.BoundsType);
            hash.Add(this.BoundsWidth);
            hash.Add(this.CropBottom);
            hash.Add(this.CropLeft);
            hash.Add(this.CropRight);
            hash.Add(this.CropTop);
            hash.Add(this.CropToBounds);
            hash.Add(this.Height);
            hash.Add(this.PositionX);
            hash.Add(this.PositionY);
            hash.Add(this.Rotation);
            hash.Add(this.ScaleX);
            hash.Add(this.ScaleY);
            hash.Add(this.SourceHeight);
            hash.Add(this.SourceWidth);
            hash.Add(this.Width);
            return hash.ToHashCode();
        }
    }
}
