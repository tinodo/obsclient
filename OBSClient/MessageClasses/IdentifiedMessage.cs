namespace OBSStudioClient.MessageClasses
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class IdentifiedMessage : IMessageData
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
