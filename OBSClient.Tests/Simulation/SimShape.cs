namespace OBSStudioClient.Tests.Simulation
{
    using System.Globalization;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using OBSStudioClient.Tests.Conformance;
    using OBSStudioClient.TestApp.Analysis;

    /// <summary>
    /// Checks the messages of a simulated OBS Studio against what is known of the real one: the protocol snapshot of the tests
    /// (<see cref="ProtocolSnapshot"/>: the names, the types and the categories of the events, the fields of the requests and their
    /// responses, the status codes), and the capture of a real OBS Studio 33.0.0 (what the protocol only describes in prose: the members of
    /// the objects in a list, and the members that OBS Studio sends beyond the protocol). Nothing is allowed that is not in one of the two,
    /// except what is listed in this class, each with its reason.
    /// </summary>
    internal sealed class SimShape
    {
        private const string CaptureName = "obs-capture-33.0.0-websocket-5.7.6.json";

        /// <summary>
        /// The category (the <c>eventSubscription</c>, in tools/protocol/obs-websocket-protocol.json) of each event that the simulation emits, which
        /// the <c>eventIntent</c> of the event has to be the bit of. The simulation has a table of its own; this one is kept apart on purpose.
        /// </summary>
        internal static readonly IReadOnlyDictionary<string, string> EventCategories = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["CurrentSceneCollectionChanging"] = "Config",
            ["CurrentSceneCollectionChanged"] = "Config",
            ["CurrentProfileChanging"] = "Config",
            ["CurrentProfileChanged"] = "Config",
            ["ProfileListChanged"] = "Config",
            ["SceneCreated"] = "Scenes",
            ["SceneRemoved"] = "Scenes",
            ["SceneNameChanged"] = "Scenes",
            ["SceneListChanged"] = "Scenes",
            ["CurrentProgramSceneChanged"] = "Scenes",
            ["CurrentPreviewSceneChanged"] = "Scenes",
            ["StudioModeStateChanged"] = "Ui",
            ["InputCreated"] = "Inputs",
            ["InputRemoved"] = "Inputs",
            ["InputNameChanged"] = "Inputs",
            ["InputMuteStateChanged"] = "Inputs",
            ["InputVolumeChanged"] = "Inputs",
            ["InputSettingsChanged"] = "Inputs",
            ["InputAudioBalanceChanged"] = "Inputs",
            ["InputAudioSyncOffsetChanged"] = "Inputs",
            ["InputAudioMonitorTypeChanged"] = "Inputs",
            ["InputAudioTracksChanged"] = "Inputs",
            ["MediaInputPlaybackStarted"] = "MediaInputs",
            ["MediaInputPlaybackEnded"] = "MediaInputs",
            ["MediaInputActionTriggered"] = "MediaInputs",
            ["SceneTransitionStarted"] = "Transitions",
            ["SceneTransitionEnded"] = "Transitions",
            ["SceneTransitionVideoEnded"] = "Transitions",
            ["CurrentSceneTransitionChanged"] = "Transitions",
            ["CurrentSceneTransitionDurationChanged"] = "Transitions",
            ["SceneItemCreated"] = "SceneItems",
            ["SceneItemRemoved"] = "SceneItems",
            ["SceneItemListReindexed"] = "SceneItems",
            ["SceneItemEnableStateChanged"] = "SceneItems",
            ["SceneItemLockStateChanged"] = "SceneItems",
            ["SceneItemSelected"] = "SceneItems",
            ["SceneItemTransformChanged"] = "SceneItemTransformChanged",
            ["InputActiveStateChanged"] = "InputActiveStateChanged",
            ["InputShowStateChanged"] = "InputShowStateChanged",
            ["InputVolumeMeters"] = "InputVolumeMeters",
        };

        /// <summary>
        /// The members of a response that a real OBS Studio sends (they are in its capture), and that the protocol snapshot does not list.
        /// <c>GetCurrentProgramScene</c> answers with <c>sceneName</c> and <c>sceneUuid</c> as the snapshot says, and also with
        /// <c>currentProgramSceneName</c> and <c>currentProgramSceneUuid</c>, as obs-websocket 5.7.6 does for the sake of old clients;
        /// <c>GetCurrentPreviewScene</c> does the same with <c>currentPreviewSceneName</c> and <c>currentPreviewSceneUuid</c> (see <see cref="ExtraResponseMembersNotCaptured"/>).
        /// </summary>
        internal static readonly IReadOnlyDictionary<string, string[]> ExtraResponseMembers = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["GetCurrentProgramScene"] = ["currentProgramSceneName", "currentProgramSceneUuid"],
            ["GetCurrentPreviewScene"] = ["currentPreviewSceneName", "currentPreviewSceneUuid"],
        };

        /// <summary>
        /// The responses of <see cref="ExtraResponseMembers"/> that the capture does not hold, because it was made with studio mode off, which refuses <c>GetCurrentPreviewScene</c>.
        /// The source of obs-websocket says that it sends the extra members, and LiveTransitionTests check it against a real OBS Studio.
        /// </summary>
        internal static readonly IReadOnlySet<string> ExtraResponseMembersNotCaptured = new HashSet<string>(StringComparer.Ordinal) { "GetCurrentPreviewScene" };

        /// <summary>
        /// The members that are null in a response whose request the capture does not hold at all, so that the capture cannot say that a real OBS Studio sends a null there, written as <c>request:path</c>.
        /// <c>GetPersistentData</c> answers with <c>slotValue</c> null for a slot that nobody put anything in (the source of obs-websocket 5.7.6, and LiveProfileSettingsModelTests against OBS Studio 33.0.0). The capture has to be the one
        /// that shows it as soon as it can: this is the way out for the answers that it lacks, and no more.
        /// </summary>
        internal static readonly IReadOnlySet<string> NullNotCaptured = new HashSet<string>(StringComparer.Ordinal) { "GetPersistentData:$.slotValue" };

        /// <summary>
        /// The members of an event that a real OBS Studio sends, and that the protocol snapshot does not list. There are none that the
        /// simulation sends.
        /// </summary>
        internal static readonly IReadOnlyDictionary<string, string[]> ExtraEventMembers = new Dictionary<string, string[]>(StringComparer.Ordinal);

        /// <summary>
        /// Kinds of JSON value, by response or event and path, that the simulation sends and the capture does not show, because the capture
        /// has no such case. A scene item of a source that is a scene has no <c>inputKind</c> (null) and has <c>isGroup</c> as a boolean
        /// (obs-websocket 5.7.6 reports <c>isGroup</c> as null for an input); the capture only holds items of inputs. A media input that plays or is paused
        /// says its length and its position as numbers (measured with OBS Studio 33.0.0: LiveMediaTests); the capture only holds one that was not opened (null).
        /// With studio mode on the list of scenes names the preview scene (<c>GetSceneList</c>), which the capture, made with studio mode off, has as null. The current transition says
        /// no duration when it is a fixed one (the cut), which the capture, made with the fade as the current transition, does not show (measured with OBS Studio 33.0.0: LiveTransitionTests).
        /// </summary>
        internal static readonly IReadOnlyDictionary<string, string[]> ExtraTypes = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["GetSceneItemList:$.sceneItems[].inputKind"] = ["null"],
            ["GetSceneItemList:$.sceneItems[].isGroup"] = ["boolean"],
            ["GetMediaInputStatus:$.mediaCursor"] = ["number"],
            ["GetMediaInputStatus:$.mediaDuration"] = ["number"],
            ["GetSceneList:$.currentPreviewSceneName"] = ["string"],
            ["GetSceneList:$.currentPreviewSceneUuid"] = ["string"],
            ["GetCurrentSceneTransition:$.transitionDuration"] = ["null"],
        };

        /// <summary>Members whose value depends on the kind of the input, so that only their own type is checked, and not what is inside.</summary>
        private static readonly string[] OpaqueMembers = ["inputSettings", "defaultInputSettings"];

        private static readonly Regex Uuid = new("^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly string[] IntegerMembers = ["sceneIndex", "sceneItemId", "sceneItemIndex", "inputKindCaps"];

        private static readonly Lazy<SimShape> Shared = new(() => new SimShape());

        private readonly Dictionary<string, Dictionary<string, PathObservation>> _responses = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<string, PathObservation>> _events = new(StringComparer.Ordinal);

        private SimShape()
        {
            using var stream = typeof(SimShape).Assembly.GetManifestResourceStream(CaptureName)
                ?? throw new InvalidOperationException($"The embedded resource '{CaptureName}' was not found.");
            using var document = JsonDocument.Parse(stream);
            foreach (var response in document.RootElement.GetProperty("responses").EnumerateArray())
            {
                Observe(_responses, response.GetProperty("requestType").GetString()!, response.GetProperty("responseData"));
            }

            foreach (var captured in document.RootElement.GetProperty("events").EnumerateArray())
            {
                Observe(_events, captured.GetProperty("eventType").GetString()!, captured.GetProperty("eventData"));
            }
        }

        /// <summary>Gets the checker, which reads the capture once.</summary>
        public static SimShape Instance => Shared.Value;

        /// <summary>Gets the types of the requests that the capture has a response to.</summary>
        public IEnumerable<string> CapturedRequests => _responses.Keys;

        /// <summary>Gets the types of the events that the capture has.</summary>
        public IEnumerable<string> CapturedEvents => _events.Keys;

        /// <summary>Tells whether the capture has a member, by its path, in the response to a request or in an event.</summary>
        /// <param name="type">The type of the request or of the event.</param>
        /// <param name="path">The path of the member, as <see cref="JsonShape"/> writes it: <c>$.sceneItems[].inputKind</c>.</param>
        /// <param name="isEvent">Whether <paramref name="type"/> is the type of an event.</param>
        public bool CapturedHas(string type, string path, bool isEvent)
        {
            return (isEvent ? _events : _responses).TryGetValue(type, out var paths) && paths.ContainsKey(path);
        }

        /// <summary>Lists what is wrong with a message of the simulation, or nothing.</summary>
        /// <param name="message">The message.</param>
        /// <param name="unorderedBatches">The IDs of the batches whose results are not checked against their request types: a batch that runs in parallel gives the results of one request under the type of another (fact 16).</param>
        public List<string> Check(SimMessage message, ISet<string>? unorderedBatches = null)
        {
            var problems = new List<string>();
            using var document = JsonDocument.Parse(message.Json);
            var root = document.RootElement;
            if (!Members(root, ["d", "op"], [], problems, "message"))
            {
                return problems;
            }

            if (root.GetProperty("op").GetInt32() != message.Op)
            {
                problems.Add($"message: the op is not {message.Op}.");
                return problems;
            }

            SortedKeys(root, "message", problems);
            var data = root.GetProperty("d");
            switch (message.Op)
            {
                case SimOp.Event:
                    CheckEvent(data, problems);
                    break;
                case SimOp.RequestResponse:
                    CheckResponse(data, problems, checkData: true, "response", requireId: true);
                    break;
                case SimOp.RequestBatchResponse:
                    // The IDs of the batches that are not checked are strings; the ID of a batch can be of any kind.
                    var unordered = unorderedBatches is not null
                        && data.ValueKind == JsonValueKind.Object
                        && data.TryGetProperty("requestId", out var id)
                        && id.ValueKind == JsonValueKind.String
                        && unorderedBatches.Contains(id.GetString()!);
                    CheckBatch(data, problems, !unordered);
                    break;
                default:
                    problems.Add($"message: the op {message.Op} is not one that OBS Studio sends after the handshake.");
                    break;
            }

            return problems;
        }

        private static void Observe(Dictionary<string, Dictionary<string, PathObservation>> into, string type, JsonElement data)
        {
            if (!into.TryGetValue(type, out var paths))
            {
                paths = new Dictionary<string, PathObservation>(StringComparer.Ordinal);
                into[type] = paths;
            }

            JsonShape.Observe("$", data, paths);
        }

        private static bool Members(JsonElement element, string[] required, string[] optional, List<string> problems, string at)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                problems.Add($"{at}: is {JsonShape.TypeName(element)}, and not an object.");
                return false;
            }

            var names = element.EnumerateObject().Select(member => member.Name).ToArray();
            var missing = required.Where(name => !names.Contains(name, StringComparer.Ordinal)).ToArray();
            foreach (var name in missing)
            {
                problems.Add($"{at}: has no member `{name}`.");
            }

            foreach (var name in names.Where(name => !required.Contains(name, StringComparer.Ordinal) && !optional.Contains(name, StringComparer.Ordinal)))
            {
                problems.Add($"{at}: has a member `{name}` that is not part of it.");
            }

            return missing.Length == 0;
        }

        // obs-websocket writes its JSON with nlohmann::json, whose objects are sorted by the bytes of the names.
        private static void SortedKeys(JsonElement element, string at, List<string> problems)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var names = element.EnumerateObject().Select(member => member.Name).ToArray();
                    if (!names.SequenceEqual(names.OrderBy(name => name, SimJson.MemberOrder), StringComparer.Ordinal))
                    {
                        problems.Add($"{at}: the members are not sorted: {string.Join(", ", names)}.");
                    }

                    foreach (var member in element.EnumerateObject())
                    {
                        SortedKeys(member.Value, at + "." + member.Name, problems);
                    }

                    break;
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        SortedKeys(item, at + "[]", problems);
                    }

                    break;
            }
        }

        private static bool IsBelowOpaque(string path)
        {
            foreach (var name in OpaqueMembers)
            {
                var prefix = "$." + name;
                if (path.Length > prefix.Length && path.StartsWith(prefix, StringComparison.Ordinal) && path[prefix.Length] is '.' or '[')
                {
                    return true;
                }
            }

            return false;
        }

        private static string Parent(string path)
        {
            var dot = path.LastIndexOf('.');
            var bracket = path.EndsWith("[]", StringComparison.Ordinal) ? path.Length - 2 : -1;
            return bracket >= 0 ? path[..bracket] : dot >= 0 ? path[..dot] : "$";
        }

        private static bool TypeMatches(string protocolType, JsonElement value)
        {
            return protocolType switch
            {
                "String" => value.ValueKind == JsonValueKind.String,
                "Number" => value.ValueKind == JsonValueKind.Number,
                "Boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
                "Object" => value.ValueKind == JsonValueKind.Object,
                _ when protocolType.StartsWith("Array", StringComparison.Ordinal) => value.ValueKind == JsonValueKind.Array,
                _ => true,
            };
        }

        private void CheckEvent(JsonElement data, List<string> problems)
        {
            if (!Members(data, ["eventData", "eventIntent", "eventType"], [], problems, "event"))
            {
                return;
            }

            var type = data.GetProperty("eventType").GetString()!;
            var at = "event " + type;
            var protocol = ProtocolSnapshot.Current;
            if (!protocol.Events.TryGetValue(type, out var fields))
            {
                problems.Add($"{at}: is not an event of obs-websocket {protocol.Version}.");
                return;
            }

            if (!EventCategories.TryGetValue(type, out var category))
            {
                problems.Add($"{at}: has no category in the table of the test.");
            }
            else if (data.GetProperty("eventIntent").GetInt64() != protocol.Enums["EventSubscription"][category])
            {
                problems.Add($"{at}: has the intent {data.GetProperty("eventIntent").GetInt64()}, and the category {category} is {protocol.Enums["EventSubscription"][category]}.");
            }

            CheckData(at, type, data.GetProperty("eventData"), fields, ExtraEventMembers, _events, problems);
        }

        private void CheckBatch(JsonElement data, List<string> problems, bool checkResultData)
        {
            if (!Members(data, ["requestId", "results"], [], problems, "batch response"))
            {
                return;
            }

            if (data.GetProperty("results").ValueKind != JsonValueKind.Array)
            {
                problems.Add("batch response: `results` is not an array.");
                return;
            }

            var number = 0;
            foreach (var result in data.GetProperty("results").EnumerateArray())
            {
                // Within a batch the ID of a request is left out when the client did not give one.
                CheckResponse(result, problems, checkResultData, $"batch result {(++number).ToString(CultureInfo.InvariantCulture)}", requireId: false);
            }
        }

        private void CheckResponse(JsonElement data, List<string> problems, bool checkData, string at, bool requireId)
        {
            // The response to a request has the ID of the request, also when it is null: the client cannot tell which one it answers without it.
            if (!Members(data, requireId ? ["requestId", "requestStatus", "requestType"] : ["requestStatus", "requestType"], requireId ? ["responseData"] : ["requestId", "responseData"], problems, at))
            {
                return;
            }

            var type = data.GetProperty("requestType").GetString()!;
            at = $"{at} ({type})";
            var status = data.GetProperty("requestStatus");
            if (!Members(status, ["code", "result"], ["comment"], problems, at + " status"))
            {
                return;
            }
            var code = status.GetProperty("code").GetInt32();
            var succeeded = status.GetProperty("result").GetBoolean();
            var protocol = ProtocolSnapshot.Current;
            if (!protocol.Enums["RequestStatus"].Values.Contains(code))
            {
                problems.Add($"{at}: the status code {code} is not one of the protocol.");
            }

            if (succeeded != (code == SimStatus.Success))
            {
                problems.Add($"{at}: result is {succeeded} and the code is {code}.");
            }

            if (succeeded && status.TryGetProperty("comment", out _))
            {
                problems.Add($"{at}: a success has a comment.");
            }

            if (!succeeded)
            {
                if (data.TryGetProperty("responseData", out _))
                {
                    problems.Add($"{at}: a failure has response data.");
                }

                return;
            }

            if (!protocol.Requests.TryGetValue(type, out var request))
            {
                problems.Add($"{at}: is not a request of obs-websocket {protocol.Version}.");
                return;
            }

            if (!checkData)
            {
                return;
            }

            var documented = request.ResponseFields;
            if (documented.Count == 0 && !ExtraResponseMembers.ContainsKey(type))
            {
                if (data.TryGetProperty("responseData", out _))
                {
                    problems.Add($"{at}: has response data, and the protocol lists no fields.");
                }

                return;
            }

            if (!data.TryGetProperty("responseData", out var responseData))
            {
                problems.Add($"{at}: has no response data.");
                return;
            }

            CheckData(at, type, responseData, documented, ExtraResponseMembers, _responses, problems);
        }

        private void CheckData(
            string at,
            string type,
            JsonElement data,
            IReadOnlyList<ProtocolField> fields,
            IReadOnlyDictionary<string, string[]> extras,
            Dictionary<string, Dictionary<string, PathObservation>> captured,
            List<string> problems)
        {
            if (data.ValueKind != JsonValueKind.Object)
            {
                problems.Add($"{at}: the data is {JsonShape.TypeName(data)}, and not an object.");
                return;
            }

            captured.TryGetValue(type, out var real);
            foreach (var field in fields.Where(field => !field.Name.Contains('.')))
            {
                if (!data.TryGetProperty(field.Name, out var value))
                {
                    if (!field.Optional)
                    {
                        problems.Add($"{at}: has no member `{field.Name}`, which the protocol requires.");
                    }

                    continue;
                }

                if (value.ValueKind == JsonValueKind.Null)
                {
                    if (!AllowsNull($"{type}:$.{field.Name}", real, $"$.{field.Name}"))
                    {
                        problems.Add($"{at}: `{field.Name}` is null, and the capture of a real OBS Studio never has it so.");
                    }
                }
                else if (!TypeMatches(field.Type, value))
                {
                    problems.Add($"{at}: `{field.Name}` is {JsonShape.TypeName(value)}, and the protocol says {field.Type}.");
                }
            }

            var allowed = fields.Select(field => field.Name.Split('.')[0]).Concat(extras.GetValueOrDefault(type) ?? []).ToHashSet(StringComparer.Ordinal);
            foreach (var member in data.EnumerateObject().Where(member => !allowed.Contains(member.Name)))
            {
                problems.Add($"{at}: has a member `{member.Name}` that neither the protocol nor the extras of this test list.");
            }

            if (real is not null)
            {
                var seen = new Dictionary<string, PathObservation>(StringComparer.Ordinal);
                JsonShape.Observe("$", data, seen);
                CompareWithCapture(at, type, seen, real, problems);
                CheckEachObject(at, "$", data, real, seen, problems);
            }

            CheckValues(at, data, problems);
        }

        // The members that a real OBS Studio always gives an object, checked on each object of the message: what one object of an array has
        // does not make up for another one that lacks it, which the paths that were observed over the whole message cannot tell.
        private static void CheckEachObject(string at, string path, JsonElement element, Dictionary<string, PathObservation> real, Dictionary<string, PathObservation> seen, List<string> problems)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    if (!IsBelowOpaque(path) && real.TryGetValue(path, out var holder))
                    {
                        foreach (var (member, observation) in real)
                        {
                            // A member that no object of the message has is reported by CompareWithCapture already.
                            if (member != path && !member.EndsWith("[]", StringComparison.Ordinal) && Parent(member) == path && observation.Seen >= holder.Seen
                                && seen.ContainsKey(member) && !element.TryGetProperty(member[(path.Length + 1)..], out _))
                            {
                                problems.Add($"{at}: an object at {path} has no {member}, which a real OBS Studio always sends there.");
                            }
                        }
                    }

                    foreach (var member in element.EnumerateObject())
                    {
                        CheckEachObject(at, $"{path}.{member.Name}", member.Value, real, seen, problems);
                    }

                    break;
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        CheckEachObject(at, path + "[]", item, real, seen, problems);
                    }

                    break;
            }
        }

        private static bool AllowsNull(string key, Dictionary<string, PathObservation>? real, string path)
        {
            return (real is not null && real.TryGetValue(path, out var observation) && observation.Types.Contains("null"))
                || (ExtraTypes.TryGetValue(key, out var extra) && extra.Contains("null"))
                || (real is null && NullNotCaptured.Contains(key));
        }

        // The members of the objects that the protocol only describes in prose: they have to be those of the capture, as the same kinds of value.
        private static void CompareWithCapture(string at, string type, Dictionary<string, PathObservation> seen, Dictionary<string, PathObservation> real, List<string> problems)
        {
            foreach (var (path, observation) in seen)
            {
                if (IsBelowOpaque(path))
                {
                    continue;
                }

                if (!real.TryGetValue(path, out var captured))
                {
                    problems.Add($"{at}: has {path}, which is not in the capture of a real OBS Studio.");
                    continue;
                }

                var extra = ExtraTypes.GetValueOrDefault($"{type}:{path}") ?? [];
                foreach (var kind in observation.Types.Where(kind => !captured.Types.Contains(kind) && !extra.Contains(kind)))
                {
                    problems.Add($"{at}: {path} is {kind}, and the capture of a real OBS Studio has {string.Join(" or ", captured.Types)}.");
                }
            }

            // A path that ends in [] is the items of an array, which an empty array does not have: it is not a member that can be missing.
            foreach (var path in real.Keys.Where(path => path != "$" && !path.EndsWith("[]", StringComparison.Ordinal) && !IsBelowOpaque(path) && !seen.ContainsKey(path) && seen.ContainsKey(Parent(path))))
            {
                problems.Add($"{at}: has no {path}, which a real OBS Studio sends.");
            }
        }

        private static void CheckValues(string at, JsonElement element, List<string> problems)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var member in element.EnumerateObject())
                    {
                        if (member.Name.EndsWith("Uuid", StringComparison.Ordinal) && member.Value.ValueKind == JsonValueKind.String && !Uuid.IsMatch(member.Value.GetString()!))
                        {
                            problems.Add($"{at}: `{member.Name}` is \"{member.Value.GetString()}\", which is not a UUID in lower case.");
                        }

                        if (IntegerMembers.Contains(member.Name, StringComparer.Ordinal) && (member.Value.ValueKind != JsonValueKind.Number || !member.Value.TryGetInt64(out _)))
                        {
                            problems.Add($"{at}: `{member.Name}` is {member.Value.GetRawText()}, which is not a whole number.");
                        }

                        if (!OpaqueMembers.Contains(member.Name, StringComparer.Ordinal))
                        {
                            CheckValues(at, member.Value, problems);
                        }
                    }

                    break;
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        CheckValues(at, item, problems);
                    }

                    break;
            }
        }
    }
}
