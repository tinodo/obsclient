namespace OBSStudioClient.MessageClasses
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    public class HelloMessage : IMessage
    {
        [JsonPropertyName("obsWebSocketVersion")]
        public string ObsWebSocketVersion { get; }

        [JsonPropertyName("rpcVersion")]
        public int RpcVersion { get; }

        [JsonPropertyName("authentication")]
        public Authentication? Authentication { get; }

        [JsonConstructor]
        public HelloMessage(string obsWebSocketVersion, int rpcVersion, Authentication? authentication)
        {
            ObsWebSocketVersion = obsWebSocketVersion;
            RpcVersion = rpcVersion;
            Authentication = authentication;
        }
    }
}
