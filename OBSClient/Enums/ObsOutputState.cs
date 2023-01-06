namespace OBSStudioClient.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Output States.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ObsOutputState
    {
        /// <summary>
        /// Unknown.
        /// </summary>
        OBS_WEBSOCKET_OUTPUT_UNKNOWN,

        /// <summary>
        /// Starting.
        /// </summary>
        OBS_WEBSOCKET_OUTPUT_STARTING,

        /// <summary>
        /// Started.
        /// </summary>
        OBS_WEBSOCKET_OUTPUT_STARTED,

        /// <summary>
        /// Stopping.
        /// </summary>
        OBS_WEBSOCKET_OUTPUT_STOPPING,

        /// <summary>
        /// Stopped.
        /// </summary>
        OBS_WEBSOCKET_OUTPUT_STOPPED,

        /// <summary>
        /// Reconnecting.
        /// </summary>
        OBS_WEBSOCKET_OUTPUT_RECONNECTING,

        /// <summary>
        /// Reconnected.
        /// </summary>
        OBS_WEBSOCKET_OUTPUT_RECONNECTED,

        /// <summary>
        /// Paused.
        /// </summary>
        OBS_WEBSOCKET_OUTPUT_PAUSED,

        /// <summary>
        /// Resumed.
        /// </summary>
        OBS_WEBSOCKET_OUTPUT_RESUMED,
    }
}
