namespace OBSStudioClient.Enums
{
    using System.Text.Json.Serialization;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ObsOutputState
    {
        OBS_WEBSOCKET_OUTPUT_UNKNOWN,
        OBS_WEBSOCKET_OUTPUT_STARTING,
        OBS_WEBSOCKET_OUTPUT_STARTED,
        OBS_WEBSOCKET_OUTPUT_STOPPING,
        OBS_WEBSOCKET_OUTPUT_STOPPED,
        OBS_WEBSOCKET_OUTPUT_RECONNECTING,
        OBS_WEBSOCKET_OUTPUT_RECONNECTED,
        OBS_WEBSOCKET_OUTPUT_PAUSED,
        OBS_WEBSOCKET_OUTPUT_RESUMED,
    }
}
