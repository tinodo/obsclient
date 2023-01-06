namespace OBSStudioClient.MessageClasses
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Class for Hello Messages.
    /// </summary>
    public class HelloMessage : IMessage
    {
        /// <summary>
        /// Version of OBS Studio WebSockets.
        /// </summary>
        [JsonPropertyName("obsWebSocketVersion")]
        public string ObsWebSocketVersion { get; }

        /// <summary>
        /// RPC Version of OBS Studio WebSockets.
        /// </summary>
        [JsonPropertyName("rpcVersion")]
        public int RpcVersion { get; }

        /// <summary>
        /// Information about optional authentication that might be required.
        /// </summary>
        [JsonPropertyName("authentication")]
        public Authentication? Authentication { get; }

        /// <summary>
        /// Creates a new instance of a <see cref="HelloMessage"/> object.
        /// </summary>
        /// <param name="obsWebSocketVersion">Version of OBS Studio WebSockets.</param>
        /// <param name="rpcVersion">RPC Version of OBS Studio WebSockets.</param>
        /// <param name="authentication">Optional authentication information.</param>
        [JsonConstructor]
        public HelloMessage(string obsWebSocketVersion, int rpcVersion, Authentication? authentication)
        {
            this.ObsWebSocketVersion = obsWebSocketVersion;
            this.RpcVersion = rpcVersion;
            this.Authentication = authentication;
        }
    }
}
