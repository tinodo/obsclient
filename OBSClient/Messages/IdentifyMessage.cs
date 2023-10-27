namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Class for Identify Messages
    /// </summary>
    public class IdentifyMessage : IMessage
    {
        /// <summary>
        /// The requested RPC Version
        /// </summary>
        [JsonPropertyName("rpcVersion")]
        public int RpcVersion { get; }

        /// <summary>
        /// The authentication string to authenticate to OBS Studio WebSockets.
        /// </summary>
        [JsonPropertyName("authentication")]
        public string? Authentication { get; }

        /// <summary>
        /// The events to subscribe to.
        /// </summary>
        [JsonPropertyName("eventSubscriptions")]
        public EventSubscriptions EventSubscriptions { get; }

        /// <summary>
        /// Created a new instance of a <see cref="IdentifyMessage"/> object.
        /// </summary>
        /// <param name="rpcVersion">The RPC Version.</param>
        /// <param name="authentication">The Authentication string.</param>
        /// <param name="eventSubscriptions">The events to subscribe to.</param>
        [JsonConstructor]
        public IdentifyMessage(int rpcVersion, string? authentication, EventSubscriptions eventSubscriptions)
        {
            this.RpcVersion = rpcVersion;
            this.Authentication = authentication;
            this.EventSubscriptions = eventSubscriptions;
        }
    }
}
