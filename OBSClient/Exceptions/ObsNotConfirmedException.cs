namespace OBSStudioClient.Exceptions
{
    /// <summary>
    /// Represents an exception that is thrown when OBS Studio made a change (it said so), and the model could not show it: the events of OBS Studio did not show it in time,
    /// and the model could not be read again in time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The change was made. Do not send it again: a change that can not be repeated (a toggle, something that is created) would be made twice. The model goes on reading what the
    /// change was about, so <see cref="Studio.ObsStudio"/> shows what OBS Studio holds as soon as OBS Studio answers the reads again. Until then the objects show what the
    /// model knew before the change.
    /// </para>
    /// <para>
    /// Unlike <see cref="ObsUnknownOutcomeException"/> this is certain: OBS Studio made the change. Unlike <see cref="ObsNotReadyException"/> and
    /// <see cref="ObsNotConnectedException"/> the change must not be made again.
    /// </para>
    /// </remarks>
    [Serializable]
    public class ObsNotConfirmedException : ObsClientException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ObsNotConfirmedException"/> class.
        /// </summary>
        /// <param name="operation">What was done, for example "enable scene item 3 of scene 'Gaming'".</param>
        public ObsNotConfirmedException(string operation)
            : base($"OBS Studio made this change: {operation}. The model does not show it yet: the events of OBS Studio did not show it in time, and the model could not be read again in time. Do not send the change again; the model goes on reading, and shows what OBS Studio holds when OBS Studio answers.")
        {
            this.Operation = operation;
        }

        /// <summary>
        /// Gets what was done.
        /// </summary>
        public string Operation { get; } = string.Empty;
    }
}
