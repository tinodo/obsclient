namespace OBSStudioClient.Exceptions
{
    /// <summary>
    /// Represents an exception that is thrown when the OBS Studio that an <see cref="Studio.ObsStudio"/> connects to is too old for it.
    /// </summary>
    /// <remarks>
    /// The model identifies scenes and inputs by UUID, which obs-websocket has from version 5.4.0 (OBS Studio 30.1). The <see cref="ObsClient"/>
    /// itself works with older versions.
    /// </remarks>
    [Serializable]
    public class ObsVersionNotSupportedException : ObsClientException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ObsVersionNotSupportedException"/> class.
        /// </summary>
        /// <param name="obsWebSocketVersion">The version of obs-websocket that OBS Studio reported.</param>
        /// <param name="requiredVersion">The oldest version that is supported.</param>
        public ObsVersionNotSupportedException(string obsWebSocketVersion, string requiredVersion)
            : base($"OBS Studio has obs-websocket {obsWebSocketVersion}, and the studio layer needs {requiredVersion} or newer (OBS Studio 30.1 or newer), because it identifies scenes and inputs by UUID. The ObsClient itself works with this version.")
        {
            this.ObsWebSocketVersion = obsWebSocketVersion;
            this.RequiredVersion = requiredVersion;
        }

        /// <summary>
        /// Gets the version of obs-websocket that OBS Studio reported.
        /// </summary>
        public string ObsWebSocketVersion { get; } = string.Empty;

        /// <summary>
        /// Gets the oldest version of obs-websocket that is supported.
        /// </summary>
        public string RequiredVersion { get; } = string.Empty;
    }
}
