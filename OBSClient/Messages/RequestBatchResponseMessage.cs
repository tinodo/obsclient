namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Class for Request Batch Response Messages
    /// </summary>
    /// <remarks>
    /// Creates a new instance of a <see cref="RequestBatchResponseMessage"/> object.
    /// </remarks>
    /// <param name="requestId">The request identifier.</param>
    /// <param name="results">The responses to the individual requests.</param>
    [method: JsonConstructor]
    public class RequestBatchResponseMessage(string requestId, RequestResponseMessage[] results) : IMessage
    {
        /// <summary>
        /// The request identifier.
        /// </summary>
        [JsonPropertyName("requestId")]
        public string RequestId { get; } = requestId;

        /// <summary>
        /// The responses to the individual requests.
        /// </summary>
        [JsonPropertyName("results")]
        public RequestResponseMessage[] Results { get; } = results ?? [];
    }
}
