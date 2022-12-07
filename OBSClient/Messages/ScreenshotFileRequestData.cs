namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;

    internal class ScreenshotFileRequestData : ScreenshotRequestData
    {
        [JsonPropertyName("imageFilePath")]
        public string ImageFilePath { get; set; }

        public ScreenshotFileRequestData(string sourceName, string imageFormat, string imageFilePath, int? imageWidth, int? imageHeight, int? imageCompressionQuality) : base(sourceName, imageFormat, imageWidth, imageHeight, imageCompressionQuality)
        {
            this.ImageFilePath = imageFilePath;
        }
    }
}
