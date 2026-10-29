namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Interfaces;
    using OBSStudioClient.Responses;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Class for Request Response Messages
    /// </summary>
    /// <remarks>
    /// Creates a new instance of a <see cref="RequestResponseMessage"/>
    /// </remarks>
    /// <param name="requestType">The type of request this is a response to.</param>
    /// <param name="requestId">The identifier of the original request.</param>
    /// <param name="requestStatus">The status of the request.</param>
    /// <param name="rawResponseData"> The raw JSON response data</param>
    [method: JsonConstructor]
    public sealed partial class RequestResponseMessage(RequestType requestType, string requestId, RequestStatus requestStatus, JsonElement? rawResponseData) : IMessage, IJsonOnDeserialized
    {
        // The type of the response data of each request (_responseTypeMap) is generated from the protocol:
        // see Generated/RequestResponseMessage.g.cs.

        /// <summary>
        /// The type of request this is a response to.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("requestType")]
        public RequestType RequestType { get; } = requestType;

        /// <summary>
        /// The identifier of the original request.
        /// </summary>
        [JsonPropertyName("requestId")]
        public string RequestId { get; } = requestId;

        /// <summary>
        /// The status of the request.
        /// </summary>
        [JsonPropertyName("requestStatus")]
        public RequestStatus RequestStatus { get; } = requestStatus;

        /// <summary>
        /// The raw JSON response data
        /// </summary>
        [JsonPropertyName("responseData")]
        public JsonElement? RawResponseData { get; } = rawResponseData;

        /// <summary>
        /// The deserialized response data.
        /// </summary>
        [JsonIgnore]
        public IResponse? ResponseData { get; private set; }

        /// <summary>
        /// Deserializes the raw JSON response to Response Data
        /// </summary>
        /// <exception cref="NotImplementedException">Not implemented.</exception>
        /// <exception cref="NotSupportedException">The response data is unknown.</exception>
        /// <exception cref="ObsClientException">Deserialization failed</exception>
        void IJsonOnDeserialized.OnDeserialized()
        {
            if (!_responseTypeMap.TryGetValue(this.RequestType, out Type? responseType))
            {
                throw new ObsClientException("OBS Studio response could not be mapped.");
            }

            if (!this.RawResponseData.HasValue)
            {
                if (responseType == typeof(EmptyResponse))
                {
                    this.ResponseData = new EmptyResponse();
                    return;
                }

                if (this.RequestStatus.Result)
                {
                    throw new ObsClientException("OBS Studio returned an empty response.");
                }

                return;
            }

            if (JsonSerializer.Deserialize(this.RawResponseData.Value.GetRawText(), responseType) is not IResponse response)
            {
                throw new ObsClientException("OBS Studio response could not be read.");
            }

            this.ResponseData = response;
        }
    }
}
