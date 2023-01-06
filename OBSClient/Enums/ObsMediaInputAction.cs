namespace OBSStudioClient.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Media Input Actions.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ObsMediaInputAction
    {
        /// <summary>
        /// None.
        /// </summary>
        OBS_WEBSOCKET_MEDIA_INPUT_ACTION_NONE,

        /// <summary>
        /// Play.
        /// </summary>
        OBS_WEBSOCKET_MEDIA_INPUT_ACTION_PLAY,

        /// <summary>
        /// Pause.
        /// </summary>
        OBS_WEBSOCKET_MEDIA_INPUT_ACTION_PAUSE,

        /// <summary>
        /// Stop.
        /// </summary>
        OBS_WEBSOCKET_MEDIA_INPUT_ACTION_STOP,

        /// <summary>
        /// Restart.
        /// </summary>
        OBS_WEBSOCKET_MEDIA_INPUT_ACTION_RESTART,

        /// <summary>
        /// Next.
        /// </summary>
        OBS_WEBSOCKET_MEDIA_INPUT_ACTION_NEXT,

        /// <summary>
        /// Previous.
        /// </summary>
        OBS_WEBSOCKET_MEDIA_INPUT_ACTION_PREVIOUS,
    }
}
