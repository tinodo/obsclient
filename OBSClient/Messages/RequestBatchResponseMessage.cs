namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Class for Request Batch Response Messages
    /// </summary>
    public class RequestBatchResponseMessage : IMessage
    {
        /// <summary>
        /// The request identifier.
        /// </summary>
        [JsonPropertyName("requestId")]
        public string RequestId { get; }

        /// <summary>
        /// The responses to the individual requests.
        /// </summary>
        [JsonPropertyName("results")]
        public RequestResponseMessage[] Results { get; }

        /// <summary>
        /// Creates a new instance of a <see cref="RequestBatchResponseMessage"/> object.
        /// </summary>
        /// <param name="requestId">The request identifier.</param>
        /// <param name="results">The responses to the individual requests.</param>
        [JsonConstructor]
        public RequestBatchResponseMessage(string requestId, RequestResponseMessage[] results)
        {
            this.RequestId = requestId;
            this.Results = results ?? Array.Empty<RequestResponseMessage>();
        }
    }
}
