namespace OBSStudioClient.Tests.Simulation
{
    using System.Text.Json.Nodes;
    using OBSStudioClient.Tests.Conformance;

    /// <summary>A message that a simulated OBS Studio sends, as obs-websocket sends it.</summary>
    /// <param name="Sequence">The number of the message, in the order in which the simulation made them. Messages with the same <paramref name="SentAt"/> leave in this order.</param>
    /// <param name="SentAt">The virtual time at which the message leaves OBS Studio.</param>
    /// <param name="Op">The operation code: 5 for an event, 7 for the response to a request, 9 for the response to a batch.</param>
    /// <param name="Json">The complete JSON text of the message.</param>
    public sealed record SimMessage(long Sequence, TimeSpan SentAt, int Op, string Json)
    {
        /// <summary>
        /// Gets what the caller of <see cref="SimObs.Receive"/> attached to the request, or batch, that this message is the response to; null for an
        /// event. Whoever has several clients (<see cref="SimObsServer"/>) tells by it where the response goes: the ID of a request is the
        /// client's to choose, it can be of any kind, and two clients can use the same one.
        /// </summary>
        public object? Tag { get; init; }
    }

    /// <summary>
    /// A message that obs-websocket does not accept. A real OBS Studio ends the connection with a close code; the simulation, which has no
    /// connection, notes it, and <see cref="SimObsServer"/> closes the connection with that code.
    /// </summary>
    /// <param name="CloseCode">The close code, one of <see cref="SimCloseCode"/>.</param>
    /// <param name="Reason">What obs-websocket says to the client.</param>
    internal sealed record SimProtocolViolation(int CloseCode, string Reason);

    /// <summary>A message that was read: the operation code and its data (the <c>d</c> of the message).</summary>
    internal sealed record SimEnvelope(int Op, JsonObject Data);

    /// <summary>The operation codes of obs-websocket.</summary>
    internal static class SimOp
    {
        public const int Hello = 0;
        public const int Identify = 1;
        public const int Identified = 2;
        public const int Reidentify = 3;
        public const int Event = 5;
        public const int Request = 6;
        public const int RequestResponse = 7;
        public const int RequestBatch = 8;
        public const int RequestBatchResponse = 9;
    }

    /// <summary>
    /// The codes of the status of a request. A test compares them with the protocol (<c>SimObsTests</c>), so that a code that the
    /// simulation answers with is a code that obs-websocket has.
    /// </summary>
    internal static class SimStatus
    {
        public const int Success = 100;
        public const int MissingRequestType = 203;

        /// <summary>The request type is not one that the simulation (or obs-websocket) knows. The protocol calls this <c>UnknownRequestType</c>.</summary>
        public const int UnknownRequestType = 204;

        /// <summary>
        /// The server is not ready to handle the request: obs-websocket answers every request with this, and sends no event, from the moment that
        /// OBS Studio begins to change its scene collection until it has done so (or until it exits).
        /// </summary>
        public const int NotReady = 207;
        public const int MissingRequestField = 300;
        public const int MissingRequestData = 301;
        public const int InvalidRequestField = 400;
        public const int InvalidRequestFieldType = 401;
        public const int RequestFieldOutOfRange = 402;
        public const int RequestFieldEmpty = 403;
        public const int TooManyRequestFields = 404;
        public const int StudioModeNotActive = 506;
        public const int ResourceNotFound = 600;
        public const int ResourceAlreadyExists = 601;
        public const int InvalidResourceType = 602;
        public const int NotEnoughResources = 603;
        public const int InvalidResourceState = 604;
        public const int InvalidInputKind = 605;
        public const int ResourceCreationFailed = 700;
        public const int ResourceActionFailed = 701;
        public const int CannotAct = 703;
    }

    /// <summary>The close codes of obs-websocket.</summary>
    internal static class SimCloseCode
    {
        public const int MessageDecodeError = 4002;
        public const int MissingDataField = 4003;
        public const int InvalidDataFieldType = 4004;
        public const int InvalidDataFieldValue = 4005;
        public const int UnknownOpCode = 4006;
        public const int NotIdentified = 4007;
        public const int AlreadyIdentified = 4008;
        public const int AuthenticationFailed = 4009;
        public const int UnsupportedRpcVersion = 4010;
        public const int UnsupportedFeature = 4012;
    }

    /// <summary>The categories of events (the bits of an event subscription), taken from the protocol snapshot of the tests.</summary>
    internal static class SimIntent
    {
        public static readonly int General = Bit("General");
        public static readonly int Config = Bit("Config");
        public static readonly int Scenes = Bit("Scenes");
        public static readonly int Inputs = Bit("Inputs");
        public static readonly int Transitions = Bit("Transitions");
        public static readonly int SceneItems = Bit("SceneItems");
        public static readonly int MediaInputs = Bit("MediaInputs");
        public static readonly int Ui = Bit("Ui");

        /// <summary>Events of the high volume, which a connection only gets when it asks for them: the transform of a scene item, and whether an input is on the air and shown.</summary>
        public static readonly int SceneItemTransform = Bit("SceneItemTransformChanged");

        /// <summary>The high-volume event that says whether an input is on the air.</summary>
        public static readonly int InputActive = Bit("InputActiveStateChanged");

        /// <summary>The high-volume event that says whether an input is shown.</summary>
        public static readonly int InputShow = Bit("InputShowStateChanged");

        /// <summary>The high-volume event with the levels of the audio of the inputs that are on the air.</summary>
        public static readonly int InputVolumeMeters = Bit("InputVolumeMeters");

        /// <summary>Gets the subscription of a connection that did not ask for one: every event except the high-volume ones.</summary>
        public static readonly int All = Bit("All");

        private static int Bit(string name)
        {
            return (int)ProtocolSnapshot.Current.Enums["EventSubscription"][name]!.Value;
        }
    }

    /// <summary>Reads a message of a client the way obs-websocket does, before it looks at what the message asks for.</summary>
    internal static class SimProtocol
    {
        /// <summary>
        /// Reads a message. The reasons paraphrase those of obs-websocket 5.7.6 (<c>WebSocketServer::onMessage</c> and <c>ProcessMessage</c>);
        /// what matters is the close code.
        /// </summary>
        /// <param name="json">The text of the message.</param>
        /// <param name="violation">What is wrong with the message, or null.</param>
        /// <returns>The operation code and the data of the message, or null when the message is not acceptable.</returns>
        public static SimEnvelope? Read(string json, out SimProtocolViolation? violation)
        {
            violation = null;
            JsonNode? root;
            try
            {
                root = JsonNode.Parse(json);
            }
            catch (Exception exception) when (exception is System.Text.Json.JsonException or ArgumentException or InvalidOperationException)
            {
                violation = new SimProtocolViolation(SimCloseCode.MessageDecodeError, "Unable to decode Json: " + exception.Message);
                return null;
            }

            if (root is not JsonObject message)
            {
                violation = new SimProtocolViolation(SimCloseCode.MessageDecodeError, "You sent a non-object payload.");
                return null;
            }

            // obs-websocket answers both with UnknownOpCode (WebSocketServer::onMessage), not with a missing or an invalid field.
            if (!message.TryGetPropertyValue("op", out var opNode) || opNode is null)
            {
                violation = new SimProtocolViolation(SimCloseCode.UnknownOpCode, "Your request is missing an `op`.");
                return null;
            }

            if (!SimJson.TryGetNumber(opNode, out var opNumber))
            {
                violation = new SimProtocolViolation(SimCloseCode.UnknownOpCode, "Your `op` is not a number.");
                return null;
            }

            var op = (int)opNumber;
            message.TryGetPropertyValue("d", out var data);
            if (data is not JsonObject dataObject)
            {
                violation = data is null
                    ? new SimProtocolViolation(SimCloseCode.MissingDataField, "Your payload is missing data (`d`).")
                    : new SimProtocolViolation(SimCloseCode.InvalidDataFieldType, "Your payload's data (`d`) is not an object.");
                return null;
            }

            return new SimEnvelope(op, dataObject);
        }
    }
}
