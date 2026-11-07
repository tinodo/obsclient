namespace OBSStudioClient.Exceptions
{
    /// <summary>
    /// Represents an exception that is thrown when a change was sent to OBS Studio, and the model does not know whether OBS Studio made it: no answer came in time, or the
    /// connection ended before the answer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// OBS Studio may have made the change, or not. The model reads again the part that the change was about, so <see cref="Studio.ObsStudio"/> will show what OBS Studio
    /// holds. Do not send the change again without looking at it: for a change that is not repeatable (create something, toggle something) it can be made twice.
    /// </para>
    /// <para>
    /// Unlike <see cref="ObsNotReadyException"/> and <see cref="ObsNotConnectedException"/> this is not certain: those say that OBS Studio did not make the change.
    /// </para>
    /// </remarks>
    [Serializable]
    public class ObsUnknownOutcomeException : ObsClientException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ObsUnknownOutcomeException"/> class.
        /// </summary>
        /// <param name="operation">What was being done, for example "enable scene item 3 of scene 'Gaming'".</param>
        /// <param name="innerException">What went wrong: a timeout, or the end of the connection.</param>
        public ObsUnknownOutcomeException(string operation, Exception innerException)
            : base($"It is not known whether OBS Studio made this change: {operation}. The request was sent, and no answer came: {innerException.Message.TrimEnd('.')}. The model reads again what the change was about; look at it before you send the change again.", innerException)
        {
            this.Operation = operation;
        }

        /// <summary>
        /// Gets what was being done.
        /// </summary>
        public string Operation { get; } = string.Empty;
    }
}
