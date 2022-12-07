namespace OBSStudioClient.MessageClasses
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    public class IdentifyMessage : IMessageData
    {
        [JsonPropertyName("rpcVersion")]
        public int RpcVersion { get; }

        [JsonPropertyName("authentication")]
        public string? Authentication { get; }

        [JsonPropertyName("eventSubscriptions")]
        public EventSubscriptions EventSubscriptions { get; }

        [JsonConstructor]
        public IdentifyMessage(int rpcVersion, string? authentication, EventSubscriptions eventSubscriptions)
        {
            RpcVersion = rpcVersion;
            Authentication = authentication;
            EventSubscriptions = eventSubscriptions;
        }
    }
}
