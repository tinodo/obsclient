namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Class for Identify Messages
    /// </summary>
    /// <remarks>
    /// Created a new instance of a <see cref="IdentifyMessage"/> object.
    /// </remarks>
    /// <param name="rpcVersion">The RPC Version.</param>
    /// <param name="authentication">The Authentication string.</param>
    /// <param name="eventSubscriptions">The events to subscribe to.</param>
    [method: JsonConstructor]
    public class IdentifyMessage(int rpcVersion, string? authentication, EventSubscriptions eventSubscriptions) : IMessage
    {
        /// <summary>
        /// The requested RPC Version
        /// </summary>
        [JsonPropertyName("rpcVersion")]
        public int RpcVersion { get; } = rpcVersion;

        /// <summary>
        /// The authentication string to authenticate to OBS Studio WebSockets.
        /// </summary>
        [JsonPropertyName("authentication")]
        public string? Authentication { get; } = authentication;

        /// <summary>
        /// The events to subscribe to.
        /// </summary>
        [JsonPropertyName("eventSubscriptions")]
        public EventSubscriptions EventSubscriptions { get; } = eventSubscriptions;
    }
}
