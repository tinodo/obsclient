namespace OBSStudioClient.Enums
{
    using System.Text.Json.Serialization;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum Realm
    {
        OBS_WEBSOCKET_DATA_REALM_GLOBAL,
        OBS_WEBSOCKET_DATA_REALM_PROFILE,
    }
}
