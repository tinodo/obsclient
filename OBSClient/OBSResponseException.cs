namespace OBSStudioClient
{
    using OBSStudioClient.Classes;

    public class OBSResponseException : Exception
    {
        public Enums.RequestStatusCode ErrorCode { get; init; }

        public string ErrorMessage { get; init; }

        public OBSResponseException(RequestStatus requestStatus)
        {
            this.ErrorCode = requestStatus.Code;
            this.ErrorMessage = requestStatus.Comment ?? string.Empty;
        }

        public OBSResponseException(Enums.RequestStatusCode errorCode, string errorMessage)
        {
            this.ErrorCode = errorCode;
            this.ErrorMessage = errorMessage;
        }

        public override string Message => this.ErrorMessage;
    }
}
