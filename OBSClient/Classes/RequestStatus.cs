namespace OBSStudioClient.Classes
{
    using OBSStudioClient.Enums;
    using System.Text.Json.Serialization;

    public class RequestStatus
    {
        [JsonPropertyName("result")]
        public bool Result { get; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("code")]
        public RequestStatusCode Code { get; }

        [JsonPropertyName("comment")]
        public string? Comment { get; }

        [JsonConstructor]
        public RequestStatus(bool result, RequestStatusCode code, string? comment)
        {
            Result = result;
            Code = code;
            Comment = comment;
        }
    }
}
