namespace OBSStudioClient.MessageClasses
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class IdentifiedMessage : IMessage
    {
        [JsonPropertyName("negotiatedRpcVersion")]
        public int NegotiatedRpcVersion { get; }

        [JsonConstructor]
        public IdentifiedMessage(int negotiatedRpcVersion)
        {
            NegotiatedRpcVersion = negotiatedRpcVersion;
        }
    }
}
