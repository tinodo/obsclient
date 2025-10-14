namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Class for Identified Messages
    /// </summary>
    /// <remarks>
    /// Creates a new instance of a <see cref="IdentifiedMessage"/> object.
    /// </remarks>
    /// <param name="negotiatedRpcVersion">The negotiated RPC Version of OBS Studio WebSockets.</param>
    [method: JsonConstructor]
    public class IdentifiedMessage(int negotiatedRpcVersion) : IMessage
    {
        /// <summary>
        /// The negotiation OBS Studio WebSocket RPC Version.
        /// </summary>
        [JsonPropertyName("negotiatedRpcVersion")]
        public int NegotiatedRpcVersion { get; } = negotiatedRpcVersion;
    }
}
