namespace OBSStudioClient.MessageClasses
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Interfaces;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Class for Request Messages
    /// </summary>
    public class RequestMessage : IMessage, IJsonOnSerializing
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
        public JsonElement? RawRequestData { get; private set; }

        /// <summary>
        /// The deserialized request data.
        /// </summary>
        [JsonIgnore]
        public IRequest? RequestData { get; }

        /// <summary>
        /// Creates a new instance of a <see cref="RequestMessage"/> object.
        /// </summary>
        /// <param name="requestType">The type of request.</param>
        /// <param name="requestId">The identifier of the request.</param>
        /// <param name="requestData">The raw JSON request data.</param>
        [JsonConstructor]
        public RequestMessage(RequestType requestType, string requestId, JsonElement requestData)
        {
            this.RequestType = requestType;
            this.RequestId = requestId;
            this.RawRequestData = requestData;
            this.RequestData = null;
        }

        /// <summary>
        /// Creates a new instance of a <see cref="RequestMessage"/> object.
        /// </summary>
        /// <param name="requestType">The type of request.</param>
        /// <param name="requestId">The identifier of the request.</param>
        /// <param name="requestData">Dynamic object to be deserialized into request data.</param>
        public RequestMessage(RequestType requestType, string requestId, dynamic requestData)
        {
            this.RequestType = requestType;
            this.RequestId = requestId;
            this.RawRequestData = JsonSerializer.SerializeToElement(requestData);
            this.RequestData = null;
        }

        /// <summary>
        /// Creates a new instance of a <see cref="RequestMessage"/> object with empty request data.
        /// </summary>
        /// <param name="requestType">The type of request.</param>
        /// <param name="requestId">The identifier of the request.</param>
        public RequestMessage(RequestType requestType, string requestId)
        {
            this.RequestType = requestType;
            this.RequestId = requestId;
            this.RawRequestData = null;
            this.RequestData = null;
        }

        /// <summary>
        /// Serializes the Request Data object into JSON.
        /// </summary>
        public void OnSerializing()
        {
            if (this.RequestData != null)
            {
                this.RawRequestData = JsonSerializer.SerializeToElement(this.RequestData, this.RequestData.GetType());
            }
        }
    }
}
