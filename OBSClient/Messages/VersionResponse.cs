namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetVersion request.
    /// </summary>
    public class VersionResponse : IResponse
    {
        /// <summary>
        /// Gets the OBS Studio version.
        /// </summary>
        [JsonPropertyName("obsVersion")]
        public string ObsVersion { get; }

        /// <summary>
        /// Gets the OBS Studio WebSocket version.
        /// </summary>
        [JsonPropertyName("obsWebSocketVersion")]
        public string ObsWebSocketVersion { get; }

        /// <summary>
        /// Gets the OBS Studio WebSocket RPC version.
        /// </summary>
        [JsonPropertyName("rpcVersion")]
        public int RpcVersion { get; }

        /// <summary>
        /// Gets a list of available requests.
        /// </summary>
        [JsonPropertyName("availableRequests")]
        public string[] AvailableRequests { get; }

        /// <summary>
        /// Gets a list of supported image formats.
        /// </summary>
        [JsonPropertyName("supportedImageFormats")]
        public string[] SupportedImageFormats { get; }

        /// <summary>
        /// Gets the name of the platform.
        /// </summary>
        /// <remarks>
        /// Usually <c>windows</c>, <c>macos</c> or <c>ubuntu</c> (linux flavor). Not guaranteed to be any of those.
        /// </remarks>
        [JsonPropertyName("platform")]
        public string Platform { get; }

        /// <summary>
        /// Gets the description of the platform.
        /// </summary>
        /// <remarks>
        /// Could be something like <c>Windows 10 (10.0)</c>.
        /// </remarks>
        [JsonPropertyName("platformDescription")]
        public string PlatformDescription { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="VersionResponse"/> class.
        /// </summary>
        /// <param name="obsVersion">The OBS Studio version.</param>
        /// <param name="obsWebSocketVersion">The OBS Studio WebSocket version.</param>
        /// <param name="rpcVersion">The OBS Studio WebSocket RPC version.</param>
        /// <param name="availableRequests">a list of available requests.</param>
        /// <param name="supportedImageFormats">A list of supported image formats.</param>
        /// <param name="platform">The name of the platform.</param>
        /// <param name="platformDescription">The description of the platform.</param>
        [JsonConstructor]
        public VersionResponse(string obsVersion, string obsWebSocketVersion, int rpcVersion, string[] availableRequests, string[] supportedImageFormats, string platform, string platformDescription)
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
