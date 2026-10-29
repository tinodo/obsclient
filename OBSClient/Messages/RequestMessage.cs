namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Interfaces;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Class for Request Messages
    /// </summary>
    public sealed class RequestMessage : IMessage
    {
        /// <summary>
        /// The type of request.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("requestType")]
        public RequestType RequestType { get; }

        /// <summary>
        /// The identifier of the request.
        /// </summary>
        [JsonPropertyName("requestId")]
        public string RequestId { get; }

        /// <summary>
        /// The raw JSON request data.
        /// </summary>
        [JsonPropertyName("requestData")]
        public JsonElement? RequestData { get; }

        /// <summary>
        /// Creates a new instance of a <see cref="RequestMessage"/> object.
        /// </summary>
        /// <param name="requestType">The type of request.</param>
        /// <param name="requestData">The data of the request: an object whose public properties are its fields, or null.</param>
        internal RequestMessage(RequestType requestType, object? requestData)
        {
            this.RequestType = requestType;
            this.RequestId = Guid.NewGuid().ToString();
            this.RequestData = RequestPayload.ToElement(requestData);
        }

        /// <summary>
        /// Creates a new instance of a <see cref="RequestMessage"/> object.
        /// </summary>
        /// <param name="requestType">The type of request.</param>
        /// <param name="requestId">The identifier of the request.</param>
        /// <param name="requestData">The raw JSON request data.</param>
        /// <remarks>This constructor should never be called.</remarks>
        [JsonConstructor]
        public RequestMessage(RequestType requestType, string requestId, JsonElement requestData)
        {
            this.RequestType = requestType;
            this.RequestId = requestId;
            this.RequestData = requestData;
        }
    }
}
