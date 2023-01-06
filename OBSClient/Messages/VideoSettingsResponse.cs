namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetVideoSettings request.
    /// </summary>
    public class VideoSettingsResponse : IResponse
    {
        /// <summary>
        /// Gets the numeratorof the fractional frames per second value.
        /// </summary>
        [JsonPropertyName("fpsNumerator")]
        public float FpsNumerator { get; }

        /// <summary>
        /// Gets the denominator of the fractional frames per second value.
        /// </summary>
        [JsonPropertyName("fpsDenominator")]
        public float FpsDenominator { get; }

        /// <summary>
        /// Gets the width of the base (canvas) resolution in pixels.
        /// </summary>
        [JsonPropertyName("baseWidth")]
        public int BaseWidth { get; }

        /// <summary>
        /// Gets the height of the base (canvas) resolution in pixels.
        /// </summary>
        [JsonPropertyName("baseHeight")]
        public int BaseHeight { get; }

        /// <summary>
        /// Gets the width of the output resolution in pixels.
        /// </summary>
        [JsonPropertyName("outputWidth")]
        public int OutputWidth { get; }

        /// <summary>
        /// Gets the height of the output resolution in pixels.
        /// </summary>
        [JsonPropertyName("outputHeight")]
        public int OutputHeight { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="VideoSettingsResponse"/> class.
        /// </summary>
        /// <param name="fpsNumerator">The numeratorof the fractional frames per second value.</param>
        /// <param name="fpsDenominator">The denominator of the fractional frames per second value.</param>
        /// <param name="baseWidth">The width of the base (canvas) resolution in pixels.</param>
        /// <param name="baseHeight">The height of the base (canvas) resolution in pixels.</param>
        /// <param name="outputWidth">The width of the output resolution in pixels.</param>
        /// <param name="outputHeight">The height of the output resolution in pixels.</param>
        [JsonConstructor]
        public VideoSettingsResponse(float fpsNumerator, float fpsDenominator, int baseWidth, int baseHeight, int outputWidth, int outputHeight)
        {
            this.FpsNumerator = fpsNumerator;
            this.FpsDenominator = fpsDenominator;
            this.BaseWidth = baseWidth;
            this.BaseHeight = baseHeight;
            this.OutputWidth = outputWidth;
            this.OutputHeight = outputHeight;
        }
    }
}
