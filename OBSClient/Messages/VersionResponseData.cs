namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class VersionResponseData : IResponseData
    {
        [JsonPropertyName("obsVersion")]
        public string ObsVersion { get; set; }

        [JsonPropertyName("obsWebSocketVersion")]
        public string ObsWebSocketVersion { get; set; }

        [JsonPropertyName("rpcVersion")]
        public int RpcVersion { get; set; }

        [JsonPropertyName("availableRequests")]
        public string[] AvailableRequests { get; set; }

        [JsonPropertyName("supportedImageFormats")]
        public string[] SupportedImageFormats { get; set; }

        [JsonPropertyName("platform")]
        public string Platform { get; set; }

        [JsonPropertyName("platformDescription")]
        public string PlatformDescription { get; set; }

        public VersionResponseData(string obsVersion, string obsWebSocketVersion, int rpcVersion, string[] availableRequests, string[] supportedImageFormats, string platform, string platformDescription)
        {
            this.ObsVersion = obsVersion;
            this.ObsWebSocketVersion = obsWebSocketVersion;
            this.RpcVersion = rpcVersion;
            this.AvailableRequests = availableRequests ?? Array.Empty<string>();
            this.SupportedImageFormats = supportedImageFormats ?? Array.Empty<string>();
            this.Platform = platform;
            this.PlatformDescription = platformDescription;
        }


    }
}
