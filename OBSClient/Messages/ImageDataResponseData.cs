namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class ImageDataResponseData : IResponseData
    {
        [JsonPropertyName("imageData")]
        public string ImageData { get; set; }

        [JsonConstructor]
        public ImageDataResponseData(string imageData)
        {
            this.ImageData = imageData;
        }
    }
}
