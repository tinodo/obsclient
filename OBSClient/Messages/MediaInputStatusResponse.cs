namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    public class MediaInputStatusResponse : IResponse
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("mediaState")]
        public MediaState MediaState { get; set; }

        [JsonPropertyName("mediaDuration")]
        public long? MediaDuration { get; set; }

        [JsonPropertyName("mediaCursor")]
        public long? MediaCursor { get; set; }

        [JsonConstructor]
        public MediaInputStatusResponse(MediaState mediaState, long? mediaDuration, long? mediaCursor)
        {
            this.MediaState = mediaState;
            this.MediaDuration = mediaDuration;
            this.MediaCursor = mediaCursor;
        }
    }
}
