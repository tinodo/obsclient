namespace OBSStudioClient.Classes
{
    using System.Text.Json.Serialization;

    public class SceneItemTransform
    {
        [JsonPropertyName("alignment")]
        public int Alignment { get; }

        [JsonPropertyName("boundsAlignment")]
        public int BoundsAlignment { get; }

        [JsonPropertyName("boundsHeight")]
        public float BoundsHeight { get; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("boundsType")]
        public Coordinates BoundsType { get; }

        [JsonPropertyName("boundsWidth")]
        public float BoundsWidth { get; }

        [JsonPropertyName("cropBottom")]
        public int CropBottom { get; }

        [JsonPropertyName("cropLeft")]
        public int CropLeft { get; }

        [JsonPropertyName("cropRight")]
        public int CropRight { get; }

        [JsonPropertyName("cropTop")]
        public int CropTop { get; }

        [JsonPropertyName("height")]
        public float Height { get; }

        [JsonPropertyName("positionX")]
        public float PositionX { get; }

        [JsonPropertyName("positionY")]
        public float PositionY { get; }

        [JsonPropertyName("rotation")]
        public float Rotation { get; }

        [JsonPropertyName("scaleX")]
        public float ScaleX { get; }

        [JsonPropertyName("scaleY")]
        public float ScaleY { get; }

        [JsonPropertyName("sourceHeight")]
        public float SourceHeight { get; }

        [JsonPropertyName("sourceWidth")]
        public float SourceWidth { get; }

        [JsonPropertyName("width")]
        public float Width { get; }

        [JsonConstructor]
        public SceneItemTransform(int alignment, int boundsAlignment, float boundsHeight, Coordinates boundsType, float boundsWidth, int cropBottom, int cropLeft, int cropRight, int cropTop, float height, float positionX, float positionY, float rotation, float scaleX, float scaleY, float sourceHeight, float sourceWidth, float width)
        {
            Alignment = alignment;
            BoundsAlignment = boundsAlignment;
            BoundsHeight = boundsHeight;
            BoundsType = boundsType;
            BoundsWidth = boundsWidth;
            CropBottom = cropBottom;
            CropLeft = cropLeft;
            CropRight = cropRight;
            CropTop = cropTop;
            Height = height;
            PositionX = positionX;
            PositionY = positionY;
            Rotation = rotation;
            ScaleX = scaleX;
            ScaleY = scaleY;
            SourceHeight = sourceHeight;
            SourceWidth = sourceWidth;
            Width = width;
        }
    }
}
