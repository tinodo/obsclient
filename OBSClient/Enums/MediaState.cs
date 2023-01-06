namespace OBSStudioClient.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Media States.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MediaState
    {
        /// <summary>
        /// None.
        /// </summary>
        OBS_MEDIA_STATE_NONE,

        /// <summary>
        /// Media Playing.
        /// </summary>
        OBS_MEDIA_STATE_PLAYING,

        /// <summary>
        /// Media Opening.
        /// </summary>
        OBS_MEDIA_STATE_OPENING,

        /// <summary>
        /// Media Buffering.
        /// </summary>
        OBS_MEDIA_STATE_BUFFERING,

        /// <summary>
        /// Media Paused.
        /// </summary>
        OBS_MEDIA_STATE_PAUSED,

        /// <summary>
        /// Media Stopped.
        /// </summary>
        OBS_MEDIA_STATE_STOPPED,

        /// <summary>
        /// Media Ended.
        /// </summary>
        OBS_MEDIA_STATE_ENDED,

        /// <summary>
        /// Media Error.
        /// </summary>
        OBS_MEDIA_STATE_ERROR,
    }
}
