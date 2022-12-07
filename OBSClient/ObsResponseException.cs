namespace OBSStudioClient
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;
    using System.Runtime.Serialization;

    [Serializable]
    public class ObsResponseException : Exception
    {
        public RequestStatusCode ErrorCode { get; init; }

        public string? ErrorMessage { get; init; }

        public ObsResponseException() { }

        public ObsResponseException(RequestStatus requestStatus)
        {
            this.ErrorCode = requestStatus.Code;
            this.ErrorMessage = requestStatus.Comment;
        }

        public ObsResponseException(RequestStatusCode errorCode, string? errorMessage)
        {
            this.ErrorCode = errorCode;
            this.ErrorMessage = errorMessage;
        }

        private ObsResponseException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
        }

        public override string Message => this.ErrorMessage ?? this.ErrorCode.ToString();
    }
}
