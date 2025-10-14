namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetSourceScreenshot or SaveSoruceScreenshot request.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="ImageDataResponse"/> class.
    /// </remarks>
    /// <param name="imageData">Base64 string with image data.</param>
    [method: JsonConstructor]
    public class ImageDataResponse(string imageData) : IResponse
    {
        /// <summary>
        /// Gets the image data as a Base64 string.
        /// </summary>
        [JsonPropertyName("imageData")]
        public string ImageData { get; } = imageData;
    }
}
