namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;

    public class ScreenshotRequest
    {
        [JsonPropertyName("sourceName")]
        public string SourceName { get; set; }

        [JsonPropertyName("imageFormat")]
        public string ImageFormat { get; set; }

        [JsonPropertyName("imageWidth")]
        public int? ImageWidth { get; set; }

        [JsonPropertyName("imageHeight")]
        public int? ImageHeight { get; set; }

        [JsonPropertyName("imageCompressionQuality")]
        public int? ImageCompressionQuality { get; set; }

        public ScreenshotRequest(string sourceName, string imageFormat, int? imageWidth, int? imageHeight, int? imageCompressionQuality)
        {
            this.SourceName = sourceName;
            this.ImageFormat = imageFormat;
            this.ImageWidth = imageWidth;
            this.ImageHeight = imageHeight;
            this.ImageCompressionQuality = imageCompressionQuality;
        }
    }
}
