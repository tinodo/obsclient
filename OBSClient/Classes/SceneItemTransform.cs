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
    public class SceneItemTransform(int alignment, int boundsAlignment, float boundsHeight, Bounds boundsType, float boundsWidth, int cropBottom, int cropLeft, int cropRight, int cropTop, float height, float positionX, float positionY, float rotation, float scaleX, float scaleY, float sourceHeight, float sourceWidth, float width)
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
    }
}
