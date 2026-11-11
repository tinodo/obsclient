namespace OBSStudioClient.Model
{
    /// <summary>
    /// What an output of OBS Studio is doing.
    /// </summary>
    /// <remarks>
    /// OBS Studio announces the steps of an output with events. A read of the output can only say whether it is running (and, for the stream, whether it is reconnecting, and
    /// for the recording, whether it is paused): it cannot tell an output that is starting or stopping from one that is stopped or running, so those two steps are only known
    /// from the events.
    /// </remarks>
    public enum ObsOutputStatus
    {
        /// <summary>
        /// The output is not running.
        /// </summary>
        Stopped,

        /// <summary>
        /// OBS Studio is starting the output. A stream is connecting to the streaming service; it can take seconds, and it can end in <see cref="Stopped"/> when the service cannot be reached.
        /// </summary>
        Starting,

        /// <summary>
        /// The output is running. A stream that reconnected (<c>OBS_WEBSOCKET_OUTPUT_RECONNECTED</c>) and a recording that was resumed (<c>OBS_WEBSOCKET_OUTPUT_RESUMED</c>) are <see cref="Started"/> again too: the change from
        /// <see cref="Reconnecting"/> or <see cref="Paused"/> to <see cref="Started"/> is a come-back, not a start.
        /// </summary>
        Started,

        /// <summary>
        /// OBS Studio is stopping the output.
        /// </summary>
        Stopping,

        /// <summary>
        /// The stream lost its connection to the streaming service, and OBS Studio tries to restore it. Only the stream has this status.
        /// </summary>
        Reconnecting,

        /// <summary>
        /// The recording is paused. Only the recording has this status.
        /// </summary>
        Paused,
    }
}
