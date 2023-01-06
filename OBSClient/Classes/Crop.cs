namespace OBSStudioClient.Classes
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides a class for Crop.
    /// </summary>
    public class Crop
    {
        /// <summary>
        /// Gets the bottom coordinate. 
        /// </summary>
        [JsonPropertyName("bottom")]
        public float Bottom { get; }

        /// <summary>
        /// Gets the sign for the bottom coordinate.
        /// </summary>
        [JsonPropertyName("bottom_sign")]
        public string BottomSign { get; }

        /// <summary>
        /// Gets the left coordinate. 
        /// </summary>
        [JsonPropertyName("left")]
        public float Left { get; }

        /// <summary>
        /// Gets the sign for the left coordinate.
        /// </summary>
        [JsonPropertyName("left_sign")]
        public string LeftSign { get; }

        /// <summary>
        /// Gets the right coordinate. 
        /// </summary>
        [JsonPropertyName("right")]
        public float Right { get; }

        /// <summary>
        /// Gets the sign for the right coordinate.
        /// </summary>
        [JsonPropertyName("right_sign")]
        public string RightSign { get; }

        /// <summary>
        /// Gets the top coordinate. 
        /// </summary>
        [JsonPropertyName("top")]
        public float Top { get; }

        /// <summary>
        /// Gets the sign for the top coordinate.
        /// </summary>
        [JsonPropertyName("top_sign")]
        public string TopSign { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Crop"/> class.
        /// </summary>
        /// <param name="bottom">The bottom coordinate.</param>
        /// <param name="bottomSign">The sign for the bottom coordinate.</param>
        /// <param name="left">The left coordinate.</param>
        /// <param name="leftSign">The sign for the left coordinate.</param>
        /// <param name="right">The right coordinate.</param>
        /// <param name="rightSign">The sign for the right coordinate.</param>
        /// <param name="top">The top coordinate.</param>
        /// <param name="topSign">The sign for the top coordinate.</param>
        [JsonConstructor]
        public Crop(float bottom, string bottomSign, float left, string leftSign, float right, string rightSign, float top, string topSign)
        {
            this.Bottom = bottom;
            this.BottomSign = bottomSign;
            this.Left = left;
            this.LeftSign = leftSign;
            this.Right = right;
            this.RightSign = rightSign;
            this.Top = top;
            this.TopSign = topSign;
        }
    }
}
