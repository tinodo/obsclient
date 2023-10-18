namespace OBSStudioClient.Enums
{
    /// <summary>
    /// Request Status Codes.
    /// </summary>
    public enum RequestStatusCode
    {
        /// <summary>
        /// Unknown.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// No Error.
        /// </summary>
        NoError = 10,

        /// <summary>
        /// Success.
        /// </summary>
        Success = 100,

        /// <summary>
        /// Missing Request Type.
        /// </summary>
        MissingRequestType = 203,

        /// <summary>
        /// Unknown Request Type.
        /// </summary>
        UnknownRequestType = 204,

        /// <summary>
        /// Generic Error.
        /// </summary>
        GenericError = 205,

        /// <summary>
        /// Unsupported Request Batch Execution Type.
        /// </summary>
        UnsupportedRequestBatchExecutionType = 206,

        /// <summary>
        /// The server is not ready to handle the request.
        /// </summary>
        NotReady = 207,

        /// <summary>
        /// Missing Request Field.
        /// </summary>
        MissingRequestField = 300,

        /// <summary>
        /// Missing Request Data.
        /// </summary>
        MissingRequestData = 301,

        /// <summary>
        /// Invalid Request Field.
        /// </summary>
        InvalidRequestField = 400,

        /// <summary>
        /// Invalid Request Field Type.
        /// </summary>
        InvalidRequestFieldType = 401,

        /// <summary>
        /// Request Field Out of Range.
        /// </summary>
        RequestFieldOutOfRange = 402,

        /// <summary>
        /// Request Field Empty.
        /// </summary>
        RequestFieldEmpty = 403,

        /// <summary>
        /// Too many Request Fields.
        /// </summary>
        TooManyRequestFields = 404,

        /// <summary>
        /// Output running.
        /// </summary>
        OutputRunning = 500,

        /// <summary>
        /// Output not running.
        /// </summary>
        OutputNotRunning = 501,

        /// <summary>
        /// Output paused.
        /// </summary>
        OutputPaused = 502,

        /// <summary>
        /// Output not paused.
        /// </summary>
        OutputNotPaused = 503,

        /// <summary>
        /// Output disabled.
        /// </summary>
        OutputDisabled = 504,

        /// <summary>
        /// Studio Mode Active.
        /// </summary>
        StudioModeActive = 505,

        /// <summary>
        /// Studio Mode not Active.
        /// </summary>
        StudioModeNotActive = 506,

        /// <summary>
        /// Resource not found.
        /// </summary>
        ResourceNotFound = 600,

        /// <summary>
        /// Resource already exists.
        /// </summary>
        ResourceAlreadyExists = 601,

        /// <summary>
        /// Invalid resource type.
        /// </summary>
        InvalidResourceType = 602,

        /// <summary>
        /// Not enough resources.
        /// </summary>
        NotEnoughResources = 603,

        /// <summary>
        /// Invalid resource state.
        /// </summary>
        InvalidResourceState = 604,

        /// <summary>
        /// Invalid input kind.
        /// </summary>
        InvalidInputKind = 605,

        /// <summary>
        /// Resource not configurable.
        /// </summary>
        ResourceNotConfigurable = 606,

        /// <summary>
        /// Invalid filter kind.
        /// </summary>
        InvalidFilterKind = 607,

        /// <summary>
        /// Resource creation failed.
        /// </summary>
        ResourceCreationFailed = 700,

        /// <summary>
        /// Resource action failed.
        /// </summary>
        ResourceActionFailed = 701,

        /// <summary>
        /// Request processing failed.
        /// </summary>
        RequestProcessingFailed = 702,

        /// <summary>
        /// Cannot act.
        /// </summary>
        CannotAct = 703,
    }
}
