namespace OBSStudioClient.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Mix Type.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MixType
    {
        /// <summary>
        /// Mix Type Preview.
        /// </summary>
        OBS_WEBSOCKET_VIDEO_MIX_TYPE_PREVIEW,

        /// <summary>
        /// Mix Type Program.
        /// </summary>
        OBS_WEBSOCKET_VIDEO_MIX_TYPE_PROGRAM,

        /// <summary>
        /// Mix Type Multi-View.
        /// </summary>
        OBS_WEBSOCKET_VIDEO_MIX_TYPE_MULTIVIEW,
    }
}
