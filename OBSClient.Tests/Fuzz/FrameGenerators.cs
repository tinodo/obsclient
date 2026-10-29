namespace OBSStudioClient.Tests.Fuzz
{
    using FsCheck;
    using FsCheck.Fluent;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Tests.Conformance;
    using System.Globalization;
    using System.Text;
    using System.Text.Json;

    /// <summary>
    /// Generators of what a server could send, as JSON text: values of every shape, and frames that look more or less like the messages of
    /// the protocol, so that the fuzz tests feed the client input that nobody wrote an example for. The names of the members are the ones
    /// of the protocol (the snapshot of the conformance tests), so that a random message now and then has the shape of a real one, and
    /// what is wrong in it is one member, or its type, or its absence.
    /// </summary>
    internal static class FrameGenerators
    {
        // Names that the messages of the protocol use, and every name of a field of an event, a request or a response.
        private static readonly string[] Names = ProtocolSnapshot.Current.Events.Values.SelectMany(fields => fields)
            .Concat(ProtocolSnapshot.Current.Requests.Values.SelectMany(request => request.RequestFields.Concat(request.ResponseFields)))
            .Select(field => field.Name)
            .Concat(
            [
                "op", "d", "requestType", "requestId", "requestStatus", "result", "code", "comment", "responseData", "eventType", "eventIntent",
                "eventData", "results", "executionType", "haltOnFailure", "rpcVersion", "authentication", "challenge", "salt", "negotiatedRpcVersion",
            ])
            .Distinct()
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        private static readonly string[] EventTypes = Enum.GetNames<EventType>();

        private static readonly string[] RequestTypes = ProtocolSnapshot.Current.Requests.Keys.OrderBy(name => name, StringComparer.Ordinal).ToArray();

        // Numbers as JSON writes them, and as it is allowed to write them: signs, exponents, what a float cannot hold, and what a long cannot.
        internal static readonly string[] Numbers =
        [
            "0", "-0", "1", "-1", "0.5", "-1.25", "1e3", "1E-3", "3.4028235e38", "1e39", "-1e39", "1e999", "-1e999", "1e-999",
            "2147483647", "2147483648", "-2147483649", "9223372036854775807", "123456789012345678901234567890", "0.1e1",
        ];

        // Text that is not like the rest: empty, with a character that JSON has to escape, outside the first plane, or half of a pair.
        private static readonly string[] OddText =
        [
            string.Empty, " ", "\0", "\n", "\"", "\\", "/", "\u00e4\u00f6\u00fc", "\u20ac", "\ud83d\ude00", "\ud83d", "\ude00", "\uffff",
        ];

        /// <summary>Gets a generator of text: random, odd, or a name of the protocol.</summary>
        public static Gen<string> Text { get; } = Gen.Frequency(
            (4, ArbMap.Default.GeneratorFor<string>().Select(text => text ?? string.Empty)),
            (2, Gen.Elements(OddText)),
            (4, Gen.Elements(Names)));

        /// <summary>Gets a generator of the JSON text of a scalar: null, a boolean, a number or a string.</summary>
        public static Gen<string> Scalar { get; } = Gen.OneOf(
            Gen.Constant("null"),
            Gen.Elements("true", "false"),
            Gen.Choose(-10, 1000).Select(number => number.ToString(CultureInfo.InvariantCulture)),
            Gen.Elements(Numbers),
            Text.Select(Quote));

        /// <summary>
        /// Gets a generator of the JSON text of a value of any shape (valid JSON, which the client has to read or to refuse, whatever it holds).
        /// </summary>
        /// <param name="depth">How deep arrays and objects may go.</param>
        public static Gen<string> Value(int depth = 3)
        {
            return depth <= 0
                ? Scalar
                : Gen.Frequency((5, Scalar), (2, Array(depth - 1)), (3, Object(depth - 1)));
        }

        /// <summary>Gets a generator of an object: members with names of the protocol, or random ones, and values of any shape.</summary>
        /// <param name="depth">How deep arrays and objects may go.</param>
        public static Gen<string> Object(int depth = 2)
        {
            var key = Gen.Frequency((6, Gen.Elements(Names)), (1, Text));
            var member = from name in key from value in Value(depth) select Quote(name) + ":" + value;
            return Gen.ListOf(member).Select(members => "{" + string.Join(",", members) + "}");
        }

        /// <summary>Gets a generator of an array of values of any shape.</summary>
        /// <param name="depth">How deep arrays and objects may go.</param>
        public static Gen<string> Array(int depth = 2)
        {
            return Gen.ListOf(Value(depth)).Select(items => "[" + string.Join(",", items) + "]");
        }

        /// <summary>
        /// Gets a generator of the data of an event of the given type: the fields that the protocol documents for it, each of them sometimes
        /// missing, sometimes of another type than the documented one, and now and then a member that the event does not have.
        /// </summary>
        /// <param name="eventType">The name of the type of the event.</param>
        public static Gen<string> EventData(string eventType)
        {
            return ProtocolSnapshot.Current.Events.TryGetValue(eventType, out var fields)
                ? DocumentedObject(fields)
                : Object();
        }

        /// <summary>
        /// Gets a generator of the response data of a request of the given type, the way <see cref="EventData"/> makes the data of an event.
        /// </summary>
        /// <param name="requestType">The name of the type of the request.</param>
        public static Gen<string> ResponseData(string requestType)
        {
            return ProtocolSnapshot.Current.Requests.TryGetValue(requestType, out var request)
                ? DocumentedObject(request.ResponseFields)
                : Object();
        }

        /// <summary>
        /// Gets a generator of the frames that a server sends after the handshake: mostly messages of the protocol with something wrong in
        /// them, sometimes valid JSON of another shape, sometimes text that is not JSON, and sometimes a message that is cut off.
        /// </summary>
        public static Gen<string> Frame()
        {
            var message = Message();
            return Gen.Frequency(
                (10, message),
                (2, Value()),
                (1, Text),
                (2, from text in message from end in Gen.Choose(0, 400) select text[..Math.Min(end, text.Length)]));
        }

        /// <summary>
        /// Gets a generator of messages: <c>{"op":..., "d":...}</c> for the operation codes that a server sends after the handshake (events,
        /// responses to a request and to a batch, which the data is shaped like) and for the ones that it should not send (a client's
        /// requests, codes that do not exist, an op that is not a number). Hello and Identified are not among them: they end a handshake
        /// that is not there, and what the client does with them is not what these tests are about.
        /// </summary>
        public static Gen<string> Message()
        {
            var op = Gen.Frequency(
                (3, Gen.Constant("5")),
                (3, Gen.Constant("7")),
                (2, Gen.Constant("9")),
                (2, Gen.Elements("1", "3", "6", "8")),
                (1, Gen.Elements("-1", "4", "10", "99", "2147483647", "4294967296", "5.5", "null", "true", "\"5\"", "[5]", "{}")));

            return Gen.Frequency(
                (10, from code in op from data in DataFor(code) select Envelope(code, data)),
                (1, from data in Value() select "{\"d\":" + data + "}"),
                (1, from code in op select "{\"op\":" + code + "}"));
        }

        /// <summary>
        /// Tells whether a frame is a Hello or an Identified, the messages of a handshake. After the handshake a server does not send them,
        /// and what the client does with one (it starts to identify again, or ends the connection when it is broken) is not what the fuzz
        /// tests of the later messages are about, so they leave these out.
        /// </summary>
        /// <param name="frame">The frame.</param>
        public static bool IsHandshakeMessage(string frame)
        {
            try
            {
                using var document = JsonDocument.Parse(frame);
                return document.RootElement is { ValueKind: JsonValueKind.Object } root
                    && root.TryGetProperty("op", out var op)
                    && op.ValueKind == JsonValueKind.Number
                    && op.TryGetInt32(out var code)
                    && code is 0 or 2;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Gets the JSON text of an escaped string.</summary>
        /// <param name="text">The text.</param>
        public static string Quote(string text)
        {
            var quoted = new StringBuilder(text.Length + 2).Append('"');
            foreach (var character in text)
            {
                if (character is '"' or '\\' || character < 0x20 || character > 0x7E)
                {
                    // Every UTF-16 unit on its own, so that half of a surrogate pair is written too (which JsonSerializer refuses to).
                    quoted.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                }
                else
                {
                    quoted.Append(character);
                }
            }

            return quoted.Append('"').ToString();
        }

        private static string Envelope(string op, string data) => "{\"op\":" + op + ",\"d\":" + data + "}";

        private static Gen<string> DataFor(string op)
        {
            // Mostly the data that the message has, shaped as documented; sometimes JSON of any other shape in its place (null, a number, a list).
            return op switch
            {
                "5" => Gen.Frequency((9, EventDataOfAnyType()), (1, Value())),
                "7" => Gen.Frequency((9, ResponseDataOfAnyType()), (1, Value())),
                "9" => Gen.Frequency((9, BatchResponse()), (1, Value())),
                _ => Value(),
            };
        }

        private static Gen<string> EventDataOfAnyType()
        {
            var eventType = Gen.Frequency((8, Gen.Elements(EventTypes)), (1, Text));
            return from type in eventType
                   from intent in Gen.Frequency((4, Gen.Elements("1", "2", "4", "8", "16", "32", "64", "128", "256", "512")), (1, Value(1)))
                   from data in Gen.Frequency((8, EventData(type)), (1, Value()), (1, Gen.Constant("null")))
                   from complete in Members(
                       (4, "eventType", Gen.Constant(Quote(type))),
                       (4, "eventIntent", Gen.Constant(intent)),
                       (4, "eventData", Gen.Constant(data)))
                   select complete;
        }

        private static Gen<string> ResponseDataOfAnyType()
        {
            var requestType = Gen.Frequency((8, Gen.Elements(RequestTypes)), (1, Text));
            var status = Gen.Frequency(
                (6, Gen.Constant("{\"result\":true,\"code\":100}")),
                (2, from code in Gen.Elements("200", "300", "400", "500", "600", "700", "-1", "99999") select "{\"result\":false,\"code\":" + code + ",\"comment\":\"no\"}"),
                (1, Value(1)));
            return from type in requestType
                   from id in Gen.Frequency((3, Text.Select(Quote)), (1, Value(1)))
                   from data in Gen.Frequency((8, ResponseData(type)), (1, Value()), (1, Gen.Constant("null")))
                   from complete in Members(
                       (5, "requestType", Gen.Constant(Quote(type))),
                       (5, "requestId", Gen.Constant(id)),
                       (5, "requestStatus", status),
                       (4, "responseData", Gen.Constant(data)))
                   select complete;
        }

        private static Gen<string> BatchResponse()
        {
            var result = ResponseDataOfAnyType();
            return from id in Gen.Frequency((3, Text.Select(Quote)), (1, Value(1)))
                   from results in Gen.Frequency((5, Gen.ListOf(result).Select(items => "[" + string.Join(",", items) + "]")), (1, Value()))
                   from complete in Members(
                       (5, "requestId", Gen.Constant(id)),
                       (5, "results", Gen.Constant(results)))
                   select complete;
        }

        // An object of the members, each there with its own weight out of 6, and sometimes a member that no message has.
        private static Gen<string> Members(params (int Weight, string Name, Gen<string> Value)[] members)
        {
            var optional = members.Select(member =>
                Gen.Frequency(
                    (member.Weight, member.Value.Select(value => (string?)(Quote(member.Name) + ":" + value))),
                    (6 - member.Weight, Gen.Constant<string?>(null)))).ToArray();
            var extra = Gen.Frequency(
                (4, Gen.Constant<string?>(null)),
                (1, from name in Gen.Elements(Names) from value in Value(1) select (string?)(Quote(name) + ":" + value)));
            return from present in Sequence(optional)
                   from other in extra
                   select "{" + string.Join(",", present.Append(other).Where(member => member is not null)) + "}";
        }

        // The generators run one after the other, and their results are put in a list (a new one at every step: a generator can run again).
        private static Gen<List<T>> Sequence<T>(IEnumerable<Gen<T>> generators)
        {
            return generators.Aggregate(
                Gen.Constant(new List<T>()),
                (all, next) => from list in all from item in next select new List<T>(list) { item });
        }

        // The fields that the protocol documents: every one of them with the documented type, and sometimes missing or wrong.
        private static Gen<string> DocumentedObject(IReadOnlyList<ProtocolField> fields)
        {
            var members = fields.Select(field => (Weight: 5, field.Name, Value: DocumentedValue(field.Type))).ToArray();
            return Members(members);
        }

        // A value of the documented type of a field, or, one time in six, a value of any type.
        private static Gen<string> DocumentedValue(string type)
        {
            var good = type switch
            {
                "String" => Text.Select(Quote),
                "Number" => Gen.Frequency((3, Gen.Choose(-1000, 100000).Select(number => number.ToString(CultureInfo.InvariantCulture))), (1, Gen.Elements(Numbers))),
                "Boolean" => Gen.Elements("true", "false"),
                "Object" => Object(1),
                "Any" => Value(1),
                _ when type.StartsWith("Array", StringComparison.Ordinal) => Array(1),
                _ => Value(1),
            };
            return Gen.Frequency((5, good), (1, Value(1)));
        }
    }
}
