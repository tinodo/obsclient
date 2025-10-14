namespace OBSStudioClient.Classes
{
    using OBSStudioClient.Enums;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides a class for RequestStatus.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="RequestStatus"/> class.
    /// </remarks>
    /// <param name="result">A value indicating whether the request was successful.</param>
    /// <param name="code">The requests <see cref="RequestStatusCode"/>.</param>
    /// <param name="comment">The comment in case of an error.</param>
    [method: JsonConstructor]
    public class RequestStatus(bool result, RequestStatusCode code, string? comment)
    {
        /// <summary>
        /// Gets a value indicating whether the request was successful.
        /// </summary>
        [JsonPropertyName("result")]
        public bool Result { get; } = result;

        /// <summary>
        /// Gets the requests <see cref="RequestStatusCode"/>.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("code")]
        public RequestStatusCode Code { get; } = code;

        /// <summary>
        /// Gets the comment in case of an error.
        /// </summary>
        [JsonPropertyName("comment")]
        public string? Comment { get; } = comment;
    }
}
