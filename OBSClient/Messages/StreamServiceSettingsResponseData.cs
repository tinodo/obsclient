namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class StreamServiceSettingsResponseData : IResponseData
    {
        [JsonPropertyName("streamServiceType")]
        public string StreamServiceType { get; set; }

        [JsonPropertyName("streamServiceSettings")]
        public object StreamServiceSettings { get; set; }

        public StreamServiceSettingsResponseData(string streamServiceType, object streamServiceSettings)
        {
            this.StreamServiceType = streamServiceType;
            this.StreamServiceSettings = streamServiceSettings;
            throw new NotImplementedException(); // need to figure out what this object is!
        }
    }
}
