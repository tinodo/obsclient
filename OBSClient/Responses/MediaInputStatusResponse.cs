namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetMediaInputStatus request.
    /// </summary>
    public class MediaInputStatusResponse : IResponse
    {
        /// <summary>
        /// Gets the <see cref="MediaState"/>.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("mediaState")]
        public MediaState MediaState { get; }

        /// <summary>
        /// Gets the media duration in milliseconds or <see langword="null"/> when not playing.
        /// </summary>
        [JsonPropertyName("mediaDuration")]
        public long? MediaDuration { get; }

        /// <summary>
        /// Gets the position of the cursor in milliseconds or <see langword="null"/> when not playing.
        /// </summary>
        [JsonPropertyName("mediaCursor")]
        public long? MediaCursor { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaInputStatusResponse"/> class.
        /// </summary>
        /// <param name="mediaState">The <see cref="MediaState"/>.</param>
        /// <param name="mediaDuration">The media duration in milliseconds.</param>
        /// <param name="mediaCursor">The position of the cursor in milliseconds.</param>
        [JsonConstructor]
        public MediaInputStatusResponse(MediaState mediaState, long? mediaDuration, long? mediaCursor)
        {
            this.MediaState = mediaState;
            this.MediaDuration = mediaDuration;
            this.MediaCursor = mediaCursor;
        }
    }
}
