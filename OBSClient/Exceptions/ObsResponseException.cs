namespace OBSStudioClient.Exceptions
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;
    using System.Runtime.Serialization;

    /// <summary>
    /// Represents an exception that occured on the OBS Studio side.
    /// </summary>
    [Serializable]
    public class ObsResponseException : Exception
    {
        /// <summary>
        /// Gets the <see cref="RequestType"/> for the exception.
        /// </summary>
        public RequestStatusCode ErrorCode { get; init; }

        /// <summary>
        /// Gets the error message for the exception.
        /// </summary>
        public string? ErrorMessage { get; init; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ObsResponseException"/> class.
        /// </summary>
        public ObsResponseException() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ObsResponseException"/> class.
        /// </summary>
        /// <param name="requestStatus">The <see cref="RequestStatus"/> to get the exception information from.</param>
        public ObsResponseException(RequestStatus requestStatus)
        {
            this.ErrorCode = requestStatus.Code;
            this.ErrorMessage = requestStatus.Comment;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ObsResponseException"/> class.
        /// </summary>
        /// <param name="errorCode">The <see cref="RequestType"/> for the exception.</param>
        /// <param name="errorMessage">The error message for the exception.</param>
        public ObsResponseException(RequestStatusCode errorCode, string? errorMessage)
        {
            this.ErrorCode = errorCode;
            this.ErrorMessage = errorMessage;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ObsResponseException"/> class.
        /// </summary>
        /// <param name="info">The <see cref="SerializationInfo"/> that holds the serialized object data about the exception being thrown.</param>
        /// <param name="context">The <see cref="StreamingContext"/> that contains contextual information about the source or destination.</param>
        protected ObsResponseException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        /// <summary>
        /// Sets the <see cref="SerializationInfo"/> with information about the exception.
        /// </summary>
        /// <param name="info">The <see cref="SerializationInfo"/> that holds the serialized object data about the exception being thrown.</param>
        /// <param name="context">The <see cref="StreamingContext"/> that contains contextual information about the source or destination.</param>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
        }

        /// <summary>
        /// Gets the exception message.
        /// </summary>
        public override string Message => this.ErrorMessage ?? this.ErrorCode.ToString();
    }
}
