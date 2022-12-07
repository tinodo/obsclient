namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;

    internal class ScreenshotFileRequest : ScreenshotRequest
    {
        [JsonPropertyName("imageFilePath")]
        public string ImageFilePath { get; set; }

        public ScreenshotFileRequest(string sourceName, string imageFormat, string imageFilePath, int? imageWidth, int? imageHeight, int? imageCompressionQuality) : base(sourceName, imageFormat, imageWidth, imageHeight, imageCompressionQuality)
        {
            this.ImageFilePath = imageFilePath;
        }
    }
}
