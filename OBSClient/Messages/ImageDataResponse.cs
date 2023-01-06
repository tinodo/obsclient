namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetSourceScreenshot or SaveSoruceScreenshot request.
    /// </summary>
    public class ImageDataResponse : IResponse
    {
        /// <summary>
        /// Gets the image data as a Base64 string.
        /// </summary>
        [JsonPropertyName("imageData")]
        public string ImageData { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageDataResponse"/> class.
        /// </summary>
        /// <param name="imageData">Base64 string with image data.</param>
        [JsonConstructor]
        public ImageDataResponse(string imageData)
        {
            this.ImageData = imageData;
        }
    }
}
