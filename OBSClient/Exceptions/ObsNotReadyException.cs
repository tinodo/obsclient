namespace OBSStudioClient.Exceptions
{
    /// <summary>
    /// Represents an exception that is thrown when something is changed while OBS Studio is not ready for it: it is changing its scene collection, or it did so after
    /// the call was made. The change was not made.
    /// </summary>
    /// <remarks>
    /// <para>
    /// While OBS Studio changes its scene collection it answers every request with "not ready", and what it holds is about to be replaced. The model does not send
    /// a change then, and a change that was waiting for its turn when the collection changed is not sent either: it would be made in a collection that you did not
    /// mean. Wait until <see cref="Studio.ObsStudio.State"/> is <see cref="Studio.ObsStudioState.Synchronized"/> again, look up what you want to change in the new collection,
    /// and make the change again.
    /// </para>
    /// <para>
    /// Unlike <see cref="ObsUnknownOutcomeException"/> this is certain: OBS Studio did not make the change.
    /// </para>
    /// </remarks>
    [Serializable]
    public class ObsNotReadyException : ObsClientException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ObsNotReadyException"/> class.
        /// </summary>
        public ObsNotReadyException()
            : base("OBS Studio is changing its scene collection, and does not take changes meanwhile. Nothing was changed.")
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ObsNotReadyException"/> class.
        /// </summary>
        /// <param name="message">The error message.</param>
        public ObsNotReadyException(string message)
            : base(message)
        {
        }
    }
}
