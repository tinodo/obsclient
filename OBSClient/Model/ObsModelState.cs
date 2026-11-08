namespace OBSStudioClient.Model
{
    /// <summary>
    /// The state of an <see cref="ObsModel"/>.
    /// </summary>
    public enum ObsModelState
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
        /// The connection is made, and the model of OBS Studio is being read: for the first time, or again because OBS Studio is changing its scene collection or has
        /// changed it. The model is not complete: while OBS Studio changes the collection it holds the last that was known (see <see cref="ObsModel.IsStale"/>), and
        /// after it has changed it holds what is read of the new one so far.
        /// </summary>
        Synchronizing,

        /// <summary>
        /// The model is complete, and is kept up to date with the events of OBS Studio.
        /// </summary>
        Synchronized,

        /// <summary>
        /// The connection was lost and the client tries to restore it. The model holds what was known last (see <see cref="ObsModel.IsStale"/>).
        /// </summary>
        Reconnecting,
    }
}
