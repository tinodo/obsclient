namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Class for Hello Messages.
    /// </summary>
    /// <remarks>
    /// Creates a new instance of a <see cref="HelloMessage"/> object.
    /// </remarks>
    /// <param name="obsWebSocketVersion">Version of OBS Studio WebSockets.</param>
    /// <param name="rpcVersion">RPC Version of OBS Studio WebSockets.</param>
    /// <param name="authentication">Optional authentication information.</param>
    [method: JsonConstructor]
    public class HelloMessage(string obsWebSocketVersion, int rpcVersion, Authentication? authentication) : IMessage
    {
        /// <summary>
        /// Version of OBS Studio WebSockets.
        /// </summary>
        [JsonPropertyName("obsWebSocketVersion")]
        public string ObsWebSocketVersion { get; } = obsWebSocketVersion;

        /// <summary>
        /// RPC Version of OBS Studio WebSockets.
        /// </summary>
        [JsonPropertyName("rpcVersion")]
        public int RpcVersion { get; } = rpcVersion;

        /// <summary>
        /// Information about optional authentication that might be required.
        /// </summary>
        [JsonPropertyName("authentication")]
        public Authentication? Authentication { get; } = authentication;
    }
}
