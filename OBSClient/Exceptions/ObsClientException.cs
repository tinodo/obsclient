namespace OBSStudioClient.Exceptions
{
    using System.Runtime.Serialization;

    /// <summary>
    /// Represents an exception that occured on the OBS Studio Client side.
    /// </summary>
    [Serializable]
    public class ObsClientException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ObsClientException"/> class.
        /// </summary>
        public ObsClientException() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ObsClientException"/> class.
        /// </summary>
        /// <param name="message">The error message.</param>
        public ObsClientException(string message) : base(message) { }
    }
}
