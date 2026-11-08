namespace OBSStudioClient.Exceptions
{
    /// <summary>
    /// Represents an exception that is thrown when something is changed while OBS Studio is not ready for it: it is changing its scene collection, or it did so after
    /// the call was made, or it did not answer in time the question that the change needed answered first. The change was not made.
    /// </summary>
    /// <remarks>
    /// <para>
    /// While OBS Studio changes its scene collection it answers every request with "not ready", and what it holds is about to be replaced. The model does not send
    /// a change then, and a change that was waiting for its turn when the collection changed is not sent either: it would be made in a collection that you did not
    /// mean. Wait until <see cref="Model.ObsModel.State"/> is <see cref="Model.ObsModelState.Synchronized"/> again, look up what you want to change in the new collection,
    /// and make the change again.
    /// </para>
    /// <para>
    /// A change that has to know what OBS Studio holds before it can say what to change (the toggle of a scene item: OBS Studio has no request for it) asks OBS Studio first.
    /// When OBS Studio does not answer that question in time, nothing was sent that changes anything, and the exception is this one (the exception that was
    /// thrown while the question was asked is the <see cref="Exception.InnerException"/>).
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

        /// <summary>
        /// Initializes a new instance of the <see cref="ObsNotReadyException"/> class.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">What went wrong while OBS Studio was asked the question: a timeout.</param>
        public ObsNotReadyException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
