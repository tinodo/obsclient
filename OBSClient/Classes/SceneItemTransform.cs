namespace OBSStudioClient.Classes
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides a class for SceneItemTransform. 
    /// </summary>
    public class SceneItemTransform
    {
        /// <summary>
        /// Gets the alignment of the scene transform.
        /// </summary>
        [JsonPropertyName("alignment")]
        public int Alignment { get; }

        /// <summary>
        /// Gets the alignment of the bounds of the scene transform.
        /// </summary>
        [JsonPropertyName("boundsAlignment")]
        public int BoundsAlignment { get; }

        /// <summary>
        /// Gets the height of the bounds of the scene transform.
        /// </summary>
        [JsonPropertyName("boundsHeight")]
        public float BoundsHeight { get; }

        /// <summary>
        /// Gets the coordinated for the bounds type.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("boundsType")]
        public Coordinates BoundsType { get; }

        /// <summary>
        /// Gets the width of the scene transform bounds.
        /// </summary>
        [JsonPropertyName("boundsWidth")]
        public float BoundsWidth { get; }

        /// <summary>
        /// Gets the bottom crop.
        /// </summary>
        [JsonPropertyName("cropBottom")]
        public int CropBottom { get; }

        /// <summary>
        /// Gets the left crop.
        /// </summary>
        [JsonPropertyName("cropLeft")]
        public int CropLeft { get; }

        /// <summary>
        /// Gets the right crop.
        /// </summary>
        [JsonPropertyName("cropRight")]
        public int CropRight { get; }

        /// <summary>
        /// Gets the top crop.
        /// </summary>
        [JsonPropertyName("cropTop")]
        public int CropTop { get; }

        /// <summary>
        /// Gets the height.
        /// </summary>
        [JsonPropertyName("height")]
        public float Height { get; }

        /// <summary>
        /// Gets the x position.
        /// </summary>
        [JsonPropertyName("positionX")]
        public float PositionX { get; }

        /// <summary>
        /// Gets the y position.
        /// </summary>
        [JsonPropertyName("positionY")]
        public float PositionY { get; }

        /// <summary>
        /// Gets the rotation.
        /// </summary>
        [JsonPropertyName("rotation")]
        public float Rotation { get; }

        /// <summary>
        /// Gets the horizontal scale.
        /// </summary>
        [JsonPropertyName("scaleX")]
        public float ScaleX { get; }

        /// <summary>
        /// Gets the vertical scale.
        /// </summary>
        [JsonPropertyName("scaleY")]
        public float ScaleY { get; }

        /// <summary>
        /// Gets the source height.
        /// </summary>
        [JsonPropertyName("sourceHeight")]
        public float SourceHeight { get; }

        /// <summary>
        /// Gets the source width.
        /// </summary>
        [JsonPropertyName("sourceWidth")]
        public float SourceWidth { get; }

        /// <summary>
        /// Gets the width.
        /// </summary>
        [JsonPropertyName("width")]
        public float Width { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SceneItemTransform"/> class.
        /// </summary>
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
        [JsonConstructor]
        public SceneItemTransform(int alignment, int boundsAlignment, float boundsHeight, Coordinates boundsType, float boundsWidth, int cropBottom, int cropLeft, int cropRight, int cropTop, float height, float positionX, float positionY, float rotation, float scaleX, float scaleY, float sourceHeight, float sourceWidth, float width)
        {
            this.Alignment = alignment;
            this.BoundsAlignment = boundsAlignment;
            this.BoundsHeight = boundsHeight;
            this.BoundsType = boundsType;
            this.BoundsWidth = boundsWidth;
            this.CropBottom = cropBottom;
            this.CropLeft = cropLeft;
            this.CropRight = cropRight;
            this.CropTop = cropTop;
            this.Height = height;
            this.PositionX = positionX;
            this.PositionY = positionY;
            this.Rotation = rotation;
            this.ScaleX = scaleX;
            this.ScaleY = scaleY;
            this.SourceHeight = sourceHeight;
            this.SourceWidth = sourceWidth;
            this.Width = width;
        }
    }
}
