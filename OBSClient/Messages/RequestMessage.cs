namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Interfaces;
    using System.Runtime.CompilerServices;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Class for Request Messages
    /// </summary>
    public class RequestMessage : IMessage
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
        /// <param name="callingMethod">The caller of this method.</param>
        protected internal RequestMessage([CallerMemberName] string callingMethod = "")
        {
            if (!Enum.TryParse<RequestType>(callingMethod[3..^7], out var requestType))
            {
                throw new ArgumentException("Name has to reflect a request type.", nameof(callingMethod));
            }

            this.RequestType = requestType;
            this.RequestId = Guid.NewGuid().ToString();
            this.RequestData = null;
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

        /// <summary>
        /// Creates a new instance of a <see cref="RequestMessage"/> object.
        /// </summary>
        /// <param name="requestData">Dynamic object to be deserialized into request data.</param>
        /// <param name="callingMethod">The caller of this method.</param>
        protected internal RequestMessage(dynamic requestData, [CallerMemberName] string callingMethod = "")
        {
            if (!Enum.TryParse<RequestType>(callingMethod[3..^7], out var requestType))
            {
                throw new ArgumentException("Name has to reflect a request type.", nameof(callingMethod));
            }

            this.RequestType = requestType;
            this.RequestId = Guid.NewGuid().ToString();
            this.RequestData = JsonSerializer.SerializeToElement(requestData);
        }
    }
}
