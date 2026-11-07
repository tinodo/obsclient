namespace OBSStudioClient.Exceptions
{
    /// <summary>
    /// Represents an exception that is thrown when a change was sent to OBS Studio, and the model does not know whether OBS Studio made it: no answer came in time, the
    /// connection ended before the answer, or the answer can not be read.
    /// </summary>
    /// <remarks>
    /// <para>
    /// OBS Studio may have made the change, or not. The model reads again the part that the change was about, so <see cref="Studio.ObsStudio"/> will show what OBS Studio
    /// holds. Do not send the change again without looking at it: for a change that is not repeatable (create something, toggle something) it can be made twice.
    /// </para>
    /// <para>
    /// Unlike <see cref="ObsNotReadyException"/> and <see cref="ObsNotConnectedException"/> this is not certain: those say that OBS Studio did not make the change.
    /// </para>
    /// <para>
    /// One case is called uncertain that was in fact never sent: a change waits for its turn to be written on the connection while another message is being written, and when
    /// OBS Studio does not read that message (a stalled connection) the change gives up after <see cref="Studio.ObsStudioOptions.WriteTimeout"/>, or ends with the connection,
    /// before one byte of it was written. The client does not tell that from a change that was written and not answered, so the call ends with this exception all the same, and
    /// the message says that the request was sent. Looking at what the model shows (it reads again) is right in both cases.
    /// </para>
    /// </remarks>
    [Serializable]
    public class ObsUnknownOutcomeException : ObsClientException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ObsUnknownOutcomeException"/> class.
        /// </summary>
        /// <param name="operation">What was being done, for example "enable scene item 3 of scene 'Gaming'".</param>
        /// <param name="innerException">What went wrong: a timeout, the end of the connection, or an answer that can not be read.</param>
        public ObsUnknownOutcomeException(string operation, Exception innerException)
            : base($"It is not known whether OBS Studio made this change: {operation}. The request was sent, and no answer that can be used came: {innerException.Message.TrimEnd('.')}. The model reads again what the change was about; look at it before you send the change again.", innerException)
        {
            this.Operation = operation;
        }

        /// <summary>
        /// Gets what was being done.
        /// </summary>
        public string Operation { get; } = string.Empty;
    }
}
