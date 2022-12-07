namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class ImageDataResponse : IResponse
    {
        [JsonPropertyName("imageData")]
        public string ImageData { get; set; }

        [JsonConstructor]
        public ImageDataResponse(string imageData)
        {
            this.ImageData = imageData;
        }
    }
}
