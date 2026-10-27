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
        /// There is no connection to OBS Studio.
        /// </summary>
        Disconnected,

        /// <summary>
        /// The connection to OBS Studio is being opened.
        /// </summary>
        Connecting,

        /// <summary>
        /// The connection is open and the handshake with OBS Studio, which includes the authentication, is in progress.
        /// </summary>
        Authenticating,

        /// <summary>
        /// OBS Studio has accepted the session: requests can be sent and events are received.
        /// </summary>
        Connected,

        /// <summary>
        /// The connection is being closed.
        /// </summary>
        Disconnecting,
    }
}
