namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Class for a Request Batch Message
    /// </summary>
    public partial class RequestBatchMessage : IMessage
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
        public bool HaltOnFailure { get; }

        /// <summary>
        /// The indication of how to process multiple requests in the batch.
        /// </summary>
        [JsonPropertyName("executionType")]
        public RequestBatchExecutionType RequestBatchExecutionType { get; }

        /// <summary>
        /// The requests to execute in the batch.
        /// </summary>
        [JsonPropertyName("requests")]
        public List<RequestMessage> Requests { get; } = new();

        /*
        /// <summary>
        /// Creates a new instance of a <see cref="RequestBatchMessage"/> object.
        /// </summary>
        /// <param name="requestId">The identifier of the request.</param>
        /// <param name="haltOnFailure">Whether to stop processing requests when a failure occurs.</param>
        /// <param name="requestBatchExecutionType">The indication of how to process multiple requests in the batch.</param>
        /// <param name="requests">The requests to execute in the batch.</param>
        [JsonConstructor]
        public RequestBatchMessage(string requestId, bool haltOnFailure = false, RequestBatchExecutionType requestBatchExecutionType = RequestBatchExecutionType.SerialRealtime, RequestMessage[] requests)
        {
            this.RequestId = requestId;
            this.HaltOnFailure = haltOnFailure;
            this.RequestBatchExecutionType = requestBatchExecutionType;
            //this.Requests = requests ?? Array.Empty<RequestMessage>();
        }
        */

        public RequestBatchMessage(bool haltOnFailure = false, RequestBatchExecutionType requestBatchExecutionType = RequestBatchExecutionType.SerialRealtime)
        {
            this.RequestId = Guid.NewGuid().ToString();
            this.HaltOnFailure = haltOnFailure;
            this.RequestBatchExecutionType = requestBatchExecutionType;
            //this.Requests = requests ?? Array.Empty<RequestMessage>();
        }
    }
}
