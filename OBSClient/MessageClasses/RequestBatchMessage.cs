namespace OBSStudioClient.MessageClasses
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Class for a Request Batch Message
    /// </summary>
    public class RequestBatchMessage : IMessage
    {
        /// <summary>
        /// The identifier of the request.
        /// </summary>
        [JsonPropertyName("requestId")]
        public string RequestId { get; }

        /// <summary>
        /// Whether to stop processing requests when a failure occurs.
        /// </summary>
        [JsonPropertyName("haltOnFailure")]
        public bool? HaltOnFailure { get; }

        /// <summary>
        /// The indication of how to process multiple requests in the batch.
        /// </summary>
        [JsonPropertyName("executionType")]
        public RequestBatchExecutionType? RequestBatchExecutionType { get; }

        /// <summary>
        /// The requests to execute in the batch.
        /// </summary>
        [JsonPropertyName("requests")]
        public RequestMessage[] Requests { get; }

        /// <summary>
        /// Creates a new instance of a <see cref="RequestBatchMessage"/> object.
        /// </summary>
        /// <param name="requestId">The identifier of the request.</param>
        /// <param name="haltOnFailure">Whether to stop processing requests when a failure occurs.</param>
        /// <param name="requestBatchExecutionType">The indication of how to process multiple requests in the batch.</param>
        /// <param name="requests">The requests to execute in the batch.</param>
        [JsonConstructor]
        public RequestBatchMessage(string requestId, bool? haltOnFailure, RequestBatchExecutionType? requestBatchExecutionType, RequestMessage[] requests)
        {
            this.RequestId = requestId;
            this.HaltOnFailure = haltOnFailure;
            this.RequestBatchExecutionType = requestBatchExecutionType;
            this.Requests = requests ?? Array.Empty<RequestMessage>();
        }
    }
}
