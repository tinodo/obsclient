namespace OBSStudioClient.Enums
{
    using System.Text.Json.Serialization;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MediaState
    {
        OBS_MEDIA_STATE_NONE,
        OBS_MEDIA_STATE_PLAYING,
        OBS_MEDIA_STATE_OPENING,
        OBS_MEDIA_STATE_BUFFERING,
        OBS_MEDIA_STATE_PAUSED,
        OBS_MEDIA_STATE_STOPPED,
        OBS_MEDIA_STATE_ENDED,
        OBS_MEDIA_STATE_ERROR,
    }
}
