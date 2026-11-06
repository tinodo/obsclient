namespace OBSStudioClient.Studio
{
    /// <summary>
    /// The state of an <see cref="ObsStudio"/>.
    /// </summary>
    public enum ObsStudioState
    {
        /// <summary>
        /// There is no connection to OBS Studio.
        /// </summary>
        Disconnected,

        /// <summary>
        /// The connection is being made.
        /// </summary>
        Connecting,

        /// <summary>
        /// The connection is made, and the model of OBS Studio is being read for the first time.
        /// </summary>
        Synchronizing,

        /// <summary>
        /// The model is complete, and is kept up to date with the events of OBS Studio.
        /// </summary>
        Synchronized,

        /// <summary>
        /// The connection was lost and the client tries to restore it. The model holds what was known last (see <see cref="ObsStudio.IsStale"/>).
        /// </summary>
        Reconnecting,
    }
}
