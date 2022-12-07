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
        public EventSubscription EventSubscriptions { get; }

        [JsonConstructor]
        public IdentifyMessage(int rpcVersion, string? authentication, EventSubscription eventSubscriptions)
        {
            RpcVersion = rpcVersion;
            Authentication = authentication;
            EventSubscriptions = eventSubscriptions;
        }
    }
}
