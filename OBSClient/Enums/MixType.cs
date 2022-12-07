namespace OBSStudioClient.Enums
{
    using System.Text.Json.Serialization;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MixType
    {
        OBS_WEBSOCKET_VIDEO_MIX_TYPE_PREVIEW,
        OBS_WEBSOCKET_VIDEO_MIX_TYPE_PROGRAM,
        OBS_WEBSOCKET_VIDEO_MIX_TYPE_MULTIVIEW,
    }
}
