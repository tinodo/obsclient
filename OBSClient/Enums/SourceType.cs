namespace OBSStudioClient.Enums
{
    using System.Text.Json.Serialization;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SourceType
    {
        OBS_SOURCE_TYPE_INPUT,
        OBS_SOURCE_TYPE_FILTER,
        OBS_SOURCE_TYPE_TRANSITION,
        OBS_SOURCE_TYPE_SCENE,
    }
}
