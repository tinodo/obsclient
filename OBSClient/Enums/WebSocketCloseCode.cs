namespace OBSStudioClient.Enums
{
    /// <summary>
    /// 1000 - 1011 are System.Net.WebSockets.WebSocketCloseStatus. The others are OBS.
    /// </summary>
    public enum WebSocketCloseCode
    {
        /// <summary>
        /// For internal use only to tell the request handler not to perform any close action.
        /// </summary>
        DontClose = 0,

        /// <summary>
        /// The connection has closed after the request was fulfilled.
        /// </summary>
        NormalClosure = 1000,

        /// <summary>
        /// Indicates an endpoint is being removed. Either the server or client will become unavailable.
        /// </summary>
        EndpointUnavailable = 1001,

        /// <summary>
        /// The client or server is terminating the connection because of a protocol error.
        /// </summary>
        ProtocolError = 1002,

        /// <summary>
        /// The client or server is terminating the connection because it cannot accept the data type it received.
        /// </summary>
        InvalidMessageType = 1003,

        /// <summary>
        /// No error specified.
        /// </summary>
        Empty = 1005,

        /// <summary>
        /// The client or server is terminating the connection because it has received data inconsistent with the message type.
        /// </summary>
        InvalidPayloadData = 1007,

        /// <summary>
        /// The connection will be closed because an endpoint has received a message that violates its policy.
        /// </summary>
        PolicyViolation = 1008,

        /// <summary>
        /// The client or server is terminating the connection because it has received a message that is too big for it to process.
        /// </summary>
        MessageTooBig = 1009,

        /// <summary>
        /// The client is terminating the connection because it expected the server to negotiate an extension.
        /// </summary>
        MandatoryExtension = 1010,

        /// <summary>
        /// The connection will be closed by the server because of an error on the server.
        /// </summary>
        InternalServerError = 1011,

        /// <summary>
        /// Unknown reason, should never be used.
        /// </summary>
        UnknownReason = 4000,

        /// <summary>
        /// The server was unable to decode the incoming websocket message.
        /// </summary>
        MessageDecodeError = 4002,

        /// <summary>
        /// A data field is required but missing from the payload.
        /// </summary>
        MissingDataField = 4003,

        /// <summary>
        /// A data field's value type is invalid.
        /// </summary>
        InvalidDataFieldType = 4004,

        /// <summary>
        /// A data field's value is invalid.
        /// </summary>
        InvalidDataFieldValue = 4005,

        /// <summary>
        /// The specified op was invalid or missing.
        /// </summary>
        UnknownOpCode = 4006,

        /// <summary>
        /// The client sent a websocket message without first sending Identify message.
        /// </summary>
        NotIdentified = 4007,

        /// <summary>
        /// The client sent an Identify message while already identified.
        /// </summary>
        /// <remarks>
        /// Once a client has identified, only Reidentify may be used to change session parameters.
        /// </remarks>
        AlreadyIdentified = 4008,

        /// <summary>
        /// The authentication attempt (via Identify) failed.
        /// </summary>
        AuthenticationFailed = 4009,

        /// <summary>
        /// The server detected the usage of an old version of the obs-websocket RPC protocol.
        /// </summary>
        UnsupportedRpcVersion = 4010,

        /// <summary>
        /// The websocket session has been invalidated by the obs-websocket server.
        /// </summary>
        /// <remarks>
        /// This is the code used by the Kick button in the UI Session List. If you receive this code, you must not automatically reconnect.
        /// </remarks>
        SessionInvalidated = 4011,

        /// <summary>
        /// A requested feature is not supported due to hardware/software limitations.
        /// </summary>
        UnsupportedFeature = 4012,
    }
}
