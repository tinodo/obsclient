namespace OBSStudioClient.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Connection State.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ConnectionState
    {
        /// <summary>
        /// Disconnected
        /// </summary>
        Disconnected,

        /// <summary>
        /// Connecting
        /// </summary>
        Connecting,

        /// <summary>
        /// Authenticating
        /// </summary>
        Authenticating,

        /// <summary>
        /// Connecting
        /// </summary>
        Connected,

        /// <summary>
        /// Disconnecting
        /// </summary>
        Disconnecting,
    }
}
