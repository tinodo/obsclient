namespace OBSStudioClient.MessageClasses
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Interfaces;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    public class RequestMessage : IMessage, IJsonOnSerializing
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("requestType")]
        public RequestType RequestType { get; }

        [JsonPropertyName("requestId")]
        public string RequestId { get; }

        [JsonPropertyName("requestData")]
        public JsonElement? RawRequestData { get; private set; }

        [JsonIgnore]
        public IRequest? RequestData { get; private set; }

        [JsonConstructor]
        public RequestMessage(RequestType requestType, string requestId, JsonElement requestData)
        {
            RequestType = requestType;
            RequestId = requestId;
            RawRequestData = requestData;
            RequestData = null;
        }

        public RequestMessage(RequestType requestType, string requestId, dynamic requestData)
        {
            RequestType = requestType;
            RequestId = requestId;
            RawRequestData = JsonSerializer.SerializeToElement(requestData);
            RequestData = null;
        }

        public RequestMessage(RequestType requestType, string requestId)
        {
            RequestType = requestType;
            RequestId = requestId;
            RawRequestData = null;
            RequestData = null;
        }

        public void OnSerializing()
        {
            if (RequestData != null)
            {
                RawRequestData = JsonSerializer.SerializeToElement(RequestData, RequestData.GetType());
            }
        }
    }
}
