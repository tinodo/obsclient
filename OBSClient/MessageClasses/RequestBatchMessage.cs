namespace OBSStudioClient.MessageClasses
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    public class RequestBatchMessage : IMessageData
    {
        [JsonPropertyName("requestId")]
        public string RequestId { get; }

        [JsonPropertyName("haltOnFailure")]
        public bool? HaltOnFailure { get; }

        [JsonPropertyName("executionType")]
        public RequestBatchExecutionType? RequestBatchExecutionType { get; }

        [JsonPropertyName("requests")]
        public RequestMessage[] Requests { get; }

        [JsonConstructor]
        public RequestBatchMessage(string requestId, bool? haltOnFailure, RequestBatchExecutionType? requestBatchExecutionType, RequestMessage[] requests)
        {
            RequestId = requestId;
            HaltOnFailure = haltOnFailure;
            RequestBatchExecutionType = requestBatchExecutionType;
            Requests = requests ?? Array.Empty<RequestMessage>();
        }
    }
}
