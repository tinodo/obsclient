namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class VideoSettingsResponse : IResponse
    {
        [JsonPropertyName("fpsNumerator")]
        public float FpsNumerator { get; set; }

        [JsonPropertyName("fpsDenominator")]
        public float FpsDenominator { get; set; }

        [JsonPropertyName("baseWidth")]
        public int BaseWidth { get; set; }

        [JsonPropertyName("baseHeight")]
        public int BaseHeight { get; set; }

        [JsonPropertyName("outputWidth")]
        public int OutputWidth { get; set; }

        [JsonPropertyName("outputHeight")]
        public int OutputHeight { get; set; }

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
