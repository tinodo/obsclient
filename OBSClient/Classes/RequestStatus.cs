namespace OBSStudioClient.Classes
{
    using OBSStudioClient.Enums;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides a class for RequestStatus.
    /// </summary>
    public class RequestStatus
    {
        /// <summary>
        /// Gets a value indicating whether the request was successful.
        /// </summary>
        [JsonPropertyName("result")]
        public bool Result { get; }

        /// <summary>
        /// Gets the requests <see cref="RequestStatusCode"/>.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("code")]
        public RequestStatusCode Code { get; }

        /// <summary>
        /// Gets the comment in case of an error.
        /// </summary>
        [JsonPropertyName("comment")]
        public string? Comment { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestStatus"/> class.
        /// </summary>
        /// <param name="result">A value indicating whether the request was successful.</param>
        /// <param name="code">The requests <see cref="RequestStatusCode"/>.</param>
        /// <param name="comment">The comment in case of an error.</param>
        [JsonConstructor]
        public RequestStatus(bool result, RequestStatusCode code, string? comment)
        {
            this.Result = result;
            this.Code = code;
            this.Comment = comment;
        }
    }
}
