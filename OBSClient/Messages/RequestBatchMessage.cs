namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Interfaces;
    using System.Collections.ObjectModel;
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
        private readonly List<RequestMessage> _requests = new();

        /// <summary>
        /// The requests to execute in the batch.
        /// </summary>
        [JsonPropertyName("requests")]
        public ReadOnlyCollection<RequestMessage> Requests => _requests.AsReadOnly();

        /// <summary>
        /// Creates a new instance of a <see cref="RequestBatchMessage"/> object.
        /// </summary>
        /// <param name="haltOnFailure">Whether to stop processing requests when a failure occurs.</param>
        /// <param name="requestBatchExecutionType">The indication of how to process multiple requests in the batch.</param>
        public RequestBatchMessage(bool haltOnFailure = false, RequestBatchExecutionType requestBatchExecutionType = RequestBatchExecutionType.SerialRealtime)
        {
            this.RequestId = Guid.NewGuid().ToString();
            this.HaltOnFailure = haltOnFailure;
            this.RequestBatchExecutionType = requestBatchExecutionType;
        }
    }
}
