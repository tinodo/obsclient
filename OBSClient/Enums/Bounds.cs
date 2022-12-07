namespace OBSStudioClient.Enums
{
    using System.Text.Json.Serialization;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum Bounds
    {
        OBS_BOUNDS_STRETCH,
        OBS_BOUNDS_SCALE_INNER,
        OBS_BOUNDS_SCALE_OUTER,
        OBS_BOUNDS_SCALE_TO_WIDTH,
        OBS_BOUNDS_SCALE_TO_HEIGHT,
        OBS_BOUNDS_MAX_ONLY,
        OBS_BOUNDS_NONE
    }
}
