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

        /// <summary>
        /// Initializes a new instance of the <see cref="ObsClientException"/> class.
        /// </summary>
        /// <param name="info">The <see cref="SerializationInfo"/> that holds the serialized object data about the exception being thrown.</param>
        /// <param name="context">The <see cref="StreamingContext"/> that contains contextual information about the source or destination.</param>
        protected ObsClientException(SerializationInfo info, StreamingContext context) : base(info, context) { }

        /// <summary>
        /// Sets the <see cref="SerializationInfo"/> with information about the exception.
        /// </summary>
        /// <param name="info">The <see cref="SerializationInfo"/> that holds the serialized object data about the exception being thrown.</param>
        /// <param name="context">The <see cref="StreamingContext"/> that contains contextual information about the source or destination.</param>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
        }
    }
}
