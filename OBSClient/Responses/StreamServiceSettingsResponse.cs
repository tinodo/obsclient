namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetStreamServiceSettings request.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="StreamServiceSettingsResponse"/> class.
    /// </remarks>
    /// <param name="streamServiceType">The stream service type.</param>
    /// <param name="streamServiceSettings">The stream service settings.</param>
    [method: JsonConstructor]
    public class StreamServiceSettingsResponse(string streamServiceType, object streamServiceSettings) : IResponse
    {
        /// <summary>
        /// Gets the stream service type, like <c>rtmp_custom</c> or <c>rtmp_common</c>.
        /// </summary>
        [JsonPropertyName("streamServiceType")]
        public string StreamServiceType { get; } = streamServiceType;

        /// <summary>
        /// Gets the stream service settings.
        /// </summary>
        [JsonPropertyName("streamServiceSettings")]
        public object StreamServiceSettings { get; } = streamServiceSettings;
    }
}
