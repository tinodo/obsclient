namespace OBSStudioClient.Exceptions
{
    /// <summary>
    /// Represents an exception that is thrown when something is asked of an <see cref="Model.ObsModel"/> that needs a connection to OBS Studio, and there is none.
    /// </summary>
    [Serializable]
    public class ObsNotConnectedException : ObsClientException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ObsNotConnectedException"/> class.
        /// </summary>
        public ObsNotConnectedException() : base("There is no connection to OBS Studio.")
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ObsNotConnectedException"/> class.
        /// </summary>
        /// <param name="message">The error message.</param>
        public ObsNotConnectedException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ObsNotConnectedException"/> class.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The exception that caused this exception.</param>
        public ObsNotConnectedException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
