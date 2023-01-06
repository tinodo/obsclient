namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetStreamServiceSettings request.
    /// </summary>
    public class StreamServiceSettingsResponse : IResponse
    {
        /// <summary>
        /// Gets the stream service type, like <c>rtmp_custom</c> or <c>rtmp_common</c>.
        /// </summary>
        [JsonPropertyName("streamServiceType")]
        public string StreamServiceType { get; }

        /// <summary>
        /// Gets the stream service settings.
        /// </summary>
        [JsonPropertyName("streamServiceSettings")]
        public object StreamServiceSettings { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamServiceSettingsResponse"/> class.
        /// </summary>
        /// <param name="streamServiceType">The stream service type.</param>
        /// <param name="streamServiceSettings">The stream service settings.</param>
        [JsonConstructor]
        public StreamServiceSettingsResponse(string streamServiceType, object streamServiceSettings)
        {
            this.StreamServiceType = streamServiceType;
            this.StreamServiceSettings = streamServiceSettings;
        }
    }
}
