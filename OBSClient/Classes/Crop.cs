namespace OBSStudioClient.Classes
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides a class for Crop.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="Crop"/> class.
    /// </remarks>
    /// <param name="bottom">The bottom coordinate.</param>
    /// <param name="bottomSign">The sign for the bottom coordinate.</param>
    /// <param name="left">The left coordinate.</param>
    /// <param name="leftSign">The sign for the left coordinate.</param>
    /// <param name="right">The right coordinate.</param>
    /// <param name="rightSign">The sign for the right coordinate.</param>
    /// <param name="top">The top coordinate.</param>
    /// <param name="topSign">The sign for the top coordinate.</param>
    [method: JsonConstructor]
    public class Crop(float bottom, string bottomSign, float left, string leftSign, float right, string rightSign, float top, string topSign)
    {
        /// <summary>
        /// Gets the bottom coordinate. 
        /// </summary>
        [JsonPropertyName("bottom")]
        public float Bottom { get; } = bottom;

        /// <summary>
        /// Gets the sign for the bottom coordinate.
        /// </summary>
        [JsonPropertyName("bottom_sign")]
        public string BottomSign { get; } = bottomSign;

        /// <summary>
        /// Gets the left coordinate. 
        /// </summary>
        [JsonPropertyName("left")]
        public float Left { get; } = left;

        /// <summary>
        /// Gets the sign for the left coordinate.
        /// </summary>
        [JsonPropertyName("left_sign")]
        public string LeftSign { get; } = leftSign;

        /// <summary>
        /// Gets the right coordinate. 
        /// </summary>
        [JsonPropertyName("right")]
        public float Right { get; } = right;

        /// <summary>
        /// Gets the sign for the right coordinate.
        /// </summary>
        [JsonPropertyName("right_sign")]
        public string RightSign { get; } = rightSign;

        /// <summary>
        /// Gets the top coordinate. 
        /// </summary>
        [JsonPropertyName("top")]
        public float Top { get; } = top;

        /// <summary>
        /// Gets the sign for the top coordinate.
        /// </summary>
        [JsonPropertyName("top_sign")]
        public string TopSign { get; } = topSign;
    }
}
