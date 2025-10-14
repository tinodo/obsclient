namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetMediaInputStatus request.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="MediaInputStatusResponse"/> class.
    /// </remarks>
    /// <param name="mediaState">The <see cref="MediaState"/>.</param>
    /// <param name="mediaDuration">The media duration in milliseconds.</param>
    /// <param name="mediaCursor">The position of the cursor in milliseconds.</param>
    [method: JsonConstructor]
    public class MediaInputStatusResponse(MediaState mediaState, long? mediaDuration, long? mediaCursor) : IResponse
    {
        /// <summary>
        /// Gets the <see cref="MediaState"/>.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("mediaState")]
        public MediaState MediaState { get; } = mediaState;

        /// <summary>
        /// Gets the media duration in milliseconds or <see langword="null"/> when not playing.
        /// </summary>
        [JsonPropertyName("mediaDuration")]
        public long? MediaDuration { get; } = mediaDuration;

        /// <summary>
        /// Gets the position of the cursor in milliseconds or <see langword="null"/> when not playing.
        /// </summary>
        [JsonPropertyName("mediaCursor")]
        public long? MediaCursor { get; } = mediaCursor;
    }
}
