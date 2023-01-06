namespace OBSStudioClient.Enums
{
    using System.Text.Json.Serialization;


    /// <summary>
    /// Type of Icon
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum IconType
    {
        /// <summary>
        /// Unknown Icon.
        /// </summary>
        OBS_ICON_TYPE_UNKNOWN,

        /// <summary>
        /// Image Icon.
        /// </summary>
        OBS_ICON_TYPE_IMAGE,

        /// <summary>
        /// Color Icon.
        /// </summary>
        OBS_ICON_TYPE_COLOR,

        /// <summary>
        /// Slideshow Icon.
        /// </summary>
        OBS_ICON_TYPE_SLIDESHOW,

        /// <summary>
        /// Audio Input Icon.
        /// </summary>
        OBS_ICON_TYPE_AUDIO_INPUT,

        /// <summary>
        /// Audio Output Icon.
        /// </summary>
        OBS_ICON_TYPE_AUDIO_OUTPUT,

        /// <summary>
        /// Desktop Capture Icon.
        /// </summary>
        OBS_ICON_TYPE_DESKTOP_CAPTURE,

        /// <summary>
        /// Window Capture Icon.
        /// </summary>
        OBS_ICON_TYPE_WINDOW_CAPTURE,

        /// <summary>
        /// Game Capture Icon.
        /// </summary>
        OBS_ICON_TYPE_GAME_CAPTURE,

        /// <summary>
        /// Camera Icon.
        /// </summary>
        OBS_ICON_TYPE_CAMERA,

        /// <summary>
        /// Text icon.
        /// </summary>
        OBS_ICON_TYPE_TEXT,

        /// <summary>
        /// Media Icon.
        /// </summary>
        OBS_ICON_TYPE_MEDIA,

        /// <summary>
        /// Browser Icon.
        /// </summary>
        OBS_ICON_TYPE_BROWSER,

        /// <summary>
        /// Custom Icon.
        /// </summary>
        OBS_ICON_TYPE_CUSTOM,

        /// <summary>
        /// Process Audio Output Icon.
        /// </summary>
        OBS_ICON_TYPE_PROCESS_AUDIO_OUTPUT,
    }
}
