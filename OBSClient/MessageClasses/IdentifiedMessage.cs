namespace OBSStudioClient.MessageClasses
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Class for Identified Messages
    /// </summary>
    public class IdentifiedMessage : IMessage
    {
        /// <summary>
        /// The negotiation OBS Studio WebSocket RPC Version.
        /// </summary>
        [JsonPropertyName("negotiatedRpcVersion")]
        public int NegotiatedRpcVersion { get; }

        /// <summary>
        /// Creates a new instance of a <see cref="IdentifiedMessage"/> object.
        /// </summary>
        /// <param name="negotiatedRpcVersion">The negotiated RPC Version of OBS Studio WebSockets.</param>
        [JsonConstructor]
        public IdentifiedMessage(int negotiatedRpcVersion)
        {
            this.NegotiatedRpcVersion = negotiatedRpcVersion;
        }
    }
}
