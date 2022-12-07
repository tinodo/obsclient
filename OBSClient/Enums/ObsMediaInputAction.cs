namespace OBSStudioClient.Enums
{
    using System.Text.Json.Serialization;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ObsMediaInputAction
    {
        OBS_WEBSOCKET_MEDIA_INPUT_ACTION_NONE,
        OBS_WEBSOCKET_MEDIA_INPUT_ACTION_PLAY,
        OBS_WEBSOCKET_MEDIA_INPUT_ACTION_PAUSE,
        OBS_WEBSOCKET_MEDIA_INPUT_ACTION_STOP,
        OBS_WEBSOCKET_MEDIA_INPUT_ACTION_RESTART,
        OBS_WEBSOCKET_MEDIA_INPUT_ACTION_NEXT,
        OBS_WEBSOCKET_MEDIA_INPUT_ACTION_PREVIOUS,
    }
}
