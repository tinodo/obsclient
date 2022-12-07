namespace OBSStudioClient.MessageClasses
{
    using OBSStudioClient.Interfaces;
    using OBSStudioClient.Messages;
    using System.Text.Json.Serialization;

    public class RequestBatchResponseMessage : IMessageData
    {
        [JsonPropertyName("requestId")]
        public string RequestId { get; }

        [JsonPropertyName("results")]
        public RequestResponseMessage[] Results { get; }

        [JsonConstructor]
        public RequestBatchResponseMessage(string requestId, RequestResponseMessage[] results)
        {
            RequestId = requestId;
            Results = results ?? Array.Empty<RequestResponseMessage>();
        }
    }
}
