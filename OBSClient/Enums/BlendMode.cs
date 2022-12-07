namespace OBSStudioClient.Enums
{
    using System.Text.Json.Serialization;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum BlendMode
    {
        OBS_BLEND_NORMAL,
        OBS_BLEND_ADDITIVE,
        OBS_BLEND_SUBTRACT,
        OBS_BLEND_SCREEN,
        OBS_BLEND_MULTIPLY,
        OBS_BLEND_LIGHTEN,
        OBS_BLEND_DARKEN,
    }
}
