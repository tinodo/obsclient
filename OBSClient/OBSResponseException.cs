namespace OBSStudioClient
{
    using OBSStudioClient.Classes;

    [Serializable]
    public class ObsResponseException : Exception
    {
        public Enums.RequestStatusCode ErrorCode { get; init; }

        public string ErrorMessage { get; init; }

        public ObsResponseException(RequestStatus requestStatus)
        {
            this.ErrorCode = requestStatus.Code;
            this.ErrorMessage = requestStatus.Comment ?? string.Empty;
        }

        public ObsResponseException(Enums.RequestStatusCode errorCode, string errorMessage)
        {
            this.ErrorCode = errorCode;
            this.ErrorMessage = errorMessage;
        }

        public override string Message => this.ErrorMessage;
    }
}
