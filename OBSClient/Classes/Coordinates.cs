namespace OBSStudioClient.Classes
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides a class for Coordinates
    /// </summary>
    public class Coordinates
    {
        /// <summary>
        /// Gets the X coordinate.
        /// </summary>
        [JsonPropertyName("x")]
        public float X { get; }

        /// <summary>
        /// Gets the X-Sign.
        /// </summary>
        [JsonPropertyName("x_sign")]
        public string XSign { get; }

        /// <summary>
        /// Gets the Y coordinate.
        /// </summary>
        [JsonPropertyName("y")]
        public float Y { get; }

        /// <summary>
        /// Gets the Y-Sign.
        /// </summary>
        [JsonPropertyName("y_sign")]
        public string YSign { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Coordinates"/> class.
        /// </summary>
        /// <param name="x">The X coordinate.</param>
        /// <param name="xSign">The X-Sign.</param>
        /// <param name="y">The Y coordinate.</param>
        /// <param name="ySign">The Y-Sign.</param>
        [JsonConstructor]
        public Coordinates(float x, string xSign, float y, string ySign)
        {
            this.X = x;
            this.XSign = xSign;
            this.Y = y;
            this.YSign = ySign;
        }
    }
}
