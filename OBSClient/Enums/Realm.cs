namespace OBSStudioClient.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Realm.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum Realm
    {
        /// <summary>
        /// Global Realm.
        /// </summary>
        OBS_WEBSOCKET_DATA_REALM_GLOBAL,

        /// <summary>
        /// Profile Realm.
        /// </summary>
        OBS_WEBSOCKET_DATA_REALM_PROFILE,
    }
}
