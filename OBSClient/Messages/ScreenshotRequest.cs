namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Request Data for OBS Client Batch requests that require a sourceName, an imageFormat, an optional imageWidth, an optional imageHeight and an optional imageCompressionQuality.
    /// </summary>
    public class ScreenshotRequest
    {
        /// <summary>
        /// Gets or sets the source name.
        /// </summary>
        [JsonPropertyName("sourceName")]
        public string SourceName { get; set; }

        /// <summary>
        /// Gets or sets the image format.
        /// </summary>
        [JsonPropertyName("imageFormat")]
        public string ImageFormat { get; set; }

        /// <summary>
        /// Gets or sets the image width.
        /// </summary>
        [JsonPropertyName("imageWidth")]
        public int? ImageWidth { get; set; }

        /// <summary>
        /// Gets or sets the image height.
        /// </summary>
        [JsonPropertyName("imageHeight")]
        public int? ImageHeight { get; set; }

        /// <summary>
        /// Gets or sets the image compression quality.
        /// </summary>
        [JsonPropertyName("imageCompressionQuality")]
        public int? ImageCompressionQuality { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ScreenshotRequest"/> class.
        /// </summary>
        /// <param name="sourceName">The source name.</param>
        /// <param name="imageFormat">The image format.</param>
        /// <param name="imageWidth">The image width.</param>
        /// <param name="imageHeight">The image height.</param>
        /// <param name="imageCompressionQuality">The image compression quality.</param>
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
