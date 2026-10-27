namespace OBSStudioClient.Classes
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the video settings of a <see cref="Canvas"/>.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="CanvasVideoSettings"/> class.
    /// All fields are <see langword="null"/> when the canvas currently has no video information available.
    /// </remarks>
    /// <param name="fpsNumerator">The numerator of the fractional frames per second value.</param>
    /// <param name="fpsDenominator">The denominator of the fractional frames per second value.</param>
    /// <param name="baseWidth">The width of the base (canvas) resolution in pixels.</param>
    /// <param name="baseHeight">The height of the base (canvas) resolution in pixels.</param>
    /// <param name="outputWidth">The width of the output resolution in pixels.</param>
    /// <param name="outputHeight">The height of the output resolution in pixels.</param>
    [method: JsonConstructor]
    public class CanvasVideoSettings(float? fpsNumerator, float? fpsDenominator, int? baseWidth, int? baseHeight, int? outputWidth, int? outputHeight)
    {
        /// <summary>
        /// Gets the numerator of the fractional frames per second value.
        /// </summary>
        [JsonPropertyName("fpsNumerator")]
        public float? FpsNumerator { get; } = fpsNumerator;

        /// <summary>
        /// Gets the denominator of the fractional frames per second value.
        /// </summary>
        [JsonPropertyName("fpsDenominator")]
        public float? FpsDenominator { get; } = fpsDenominator;

        /// <summary>
        /// Gets the width of the base (canvas) resolution in pixels.
        /// </summary>
        [JsonPropertyName("baseWidth")]
        public int? BaseWidth { get; } = baseWidth;

        /// <summary>
        /// Gets the height of the base (canvas) resolution in pixels.
        /// </summary>
        [JsonPropertyName("baseHeight")]
        public int? BaseHeight { get; } = baseHeight;

        /// <summary>
        /// Gets the width of the output resolution in pixels.
        /// </summary>
        [JsonPropertyName("outputWidth")]
        public int? OutputWidth { get; } = outputWidth;

        /// <summary>
        /// Gets the height of the output resolution in pixels.
        /// </summary>
        [JsonPropertyName("outputHeight")]
        public int? OutputHeight { get; } = outputHeight;
    }
}
