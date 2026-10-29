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
                    // The whole of the response of CreateScene is the UUID of the scene, and obs-websocket before 5.4.0 (OBS Studio before
                    // 30.1.0) has no UUIDs: it sends no response data, for a scene that was made. The request did not fail, so it does not
                    // throw: the UUID is empty (the name that was asked for identifies the scene), as it is in the other responses of such an
                    // OBS Studio. Any other response without data is not what OBS Studio sends.
                    if (this.RequestType == RequestType.CreateScene)
                    {
                        this.ResponseData = new CreateSceneResponse(Guid.Empty);
                        return;
                    }

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
