namespace OBSStudioClient.Enums
{
    using System.Text.Json.Serialization;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        Authenticating,
        Connected,
        Disconnecting,
    }
}
