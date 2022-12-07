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
        public int? MediaDuration { get; set; }

        [JsonPropertyName("mediaCursor")]
        public int? MediaCursor { get; set; }

        [JsonConstructor]
        public MediaInputStatusResponse(MediaState mediaState, int? mediaDuration, int? mediaCursor)
        {
            this.MediaState = mediaState;
            this.MediaDuration = mediaDuration;
            this.MediaCursor = mediaCursor;
        }
    }
}
