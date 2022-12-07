namespace OBSStudioClient.Enums
{
    /// <summary>
    /// 1000 - 1011 are System.Net.WebSockets.WebSocketCloseStatus. The others are OBS.
    /// </summary>
    public enum WebSocketCloseCode
    {
        DontClose = 0,

        //
        // Summary:
        //     (1000) The connection has closed after the request was fulfilled.
        NormalClosure = 1000,
        //
        // Summary:
        //     (1001) Indicates an endpoint is being removed. Either the server or client will
        //     become unavailable.
        EndpointUnavailable = 1001,
        //
        // Summary:
        //     (1002) The client or server is terminating the connection because of a protocol
        //     error.
        ProtocolError = 1002,
        //
        // Summary:
        //     (1003) The client or server is terminating the connection because it cannot accept
        //     the data type it received.
        InvalidMessageType = 1003,
        //
        // Summary:
        //     No error specified.
        Empty = 1005,
        //
        // Summary:
        //     (1007) The client or server is terminating the connection because it has received
        //     data inconsistent with the message type.
        InvalidPayloadData = 1007,
        //
        // Summary:
        //     (1008) The connection will be closed because an endpoint has received a message
        //     that violates its policy.
        PolicyViolation = 1008,
        //
        // Summary:
        //     (1009) The client or server is terminating the connection because it has received
        //     a message that is too big for it to process.
        MessageTooBig = 1009,
        //
        // Summary:
        //     (1010) The client is terminating the connection because it expected the server
        //     to negotiate an extension.
        MandatoryExtension = 1010,
        //
        // Summary:
        //     (1011) The connection will be closed by the server because of an error on the
        //     server.
        InternalServerError = 1011,

        UnknownReason = 4000,
        MessageDecodeError = 4002,
        MissingDataField = 4003,
        InvalidDataFieldType = 4004,
        InvalidDataFieldValue = 4005,
        UnknownOpCode = 4006,
        NotIdentified = 4007,
        AlreadyIdentified = 4008,
        AuthenticationFailed = 4009,
        UnsupportedRpcVersion = 4010,
        SessionInvalidated = 4011,
        UnsupportedFeature = 4012,
    }
}
