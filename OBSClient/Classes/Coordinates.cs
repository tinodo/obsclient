namespace OBSStudioClient.Classes
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides a class for Coordinates
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="Coordinates"/> class.
    /// </remarks>
    /// <param name="x">The X coordinate.</param>
    /// <param name="xSign">The X-Sign.</param>
    /// <param name="y">The Y coordinate.</param>
    /// <param name="ySign">The Y-Sign.</param>
    [method: JsonConstructor]
    public class Coordinates(float x, string xSign, float y, string ySign)
    {
        /// <summary>
        /// Gets the X coordinate.
        /// </summary>
        [JsonPropertyName("x")]
        public float X { get; } = x;

        /// <summary>
        /// Gets the X-Sign.
        /// </summary>
        [JsonPropertyName("x_sign")]
        public string XSign { get; } = xSign;

        /// <summary>
        /// Gets the Y coordinate.
        /// </summary>
        [JsonPropertyName("y")]
        public float Y { get; } = y;

        /// <summary>
        /// Gets the Y-Sign.
        /// </summary>
        [JsonPropertyName("y_sign")]
        public string YSign { get; } = ySign;
    }
}
