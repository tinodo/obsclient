namespace OBSStudioClient.Tests.Simulation
{
    using System.Globalization;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using static OBSStudioClient.Tests.Simulation.SimJson;

    /// <summary>
    /// A simulated OBS Studio: it keeps a real, reference counted state of scenes, inputs and scene items, applies the requests of a
    /// client to it, and answers and announces them the way a real OBS Studio 33.0.0 with obs-websocket 5.7.6 does, quirks included
    /// (see <see cref="SimQuirks"/>). It is deterministic: time is virtual (<see cref="Clock"/>, moved by <see cref="Advance"/>) and all
    /// randomness comes from one generator that is made from the seed, so the same seed and the same calls give the same messages, byte for byte.
    /// </summary>
    /// <remarks>
    /// It has no sockets, no threads and no timers, and it is not thread safe. A message of a client reaches it with <see cref="Receive"/>
    /// at <see cref="Clock"/>; what OBS Studio sends comes out of <see cref="TakeDue"/> when virtual time has reached it. <see cref="SimObsServer"/>
    /// puts a WebSocket in front of it.
    /// </remarks>
    /// <example>
    /// <code>
    /// var sim = new SimObs(SimQuirks.Hostile, seed: 42);
    /// sim.Request("CreateScene", new { sceneName = "A" });                  // a client asks
    /// sim.External.CreateInput(sim.Snapshot().Scenes[0].Uuid, "Mic", "wasapi_input_capture");   // someone else changes something
    /// while (sim.NextDeadline is { } next)                                  // run until nothing is scheduled any more
    /// {
    ///     sim.AdvanceTo(next &gt; sim.Clock.Now ? next : sim.Clock.Now);
    ///     foreach (var message in sim.TakeDue()) { /* give message.Json to the model: events and responses, in the order they leave */ }
    /// }
    /// sim.CheckInvariants();
    /// var truth = sim.Snapshot();                                           // what the model has to equal now
    /// </code>
    /// </example>
    internal sealed class SimObs
    {
        private static readonly Dictionary<string, Func<int>> EventIntents = new(StringComparer.Ordinal)
        {
            ["SceneCreated"] = static () => SimIntent.Scenes,
            ["SceneRemoved"] = static () => SimIntent.Scenes,
            ["SceneNameChanged"] = static () => SimIntent.Scenes,
            ["SceneListChanged"] = static () => SimIntent.Scenes,
            ["CurrentProgramSceneChanged"] = static () => SimIntent.Scenes,
            ["InputCreated"] = static () => SimIntent.Inputs,
            ["InputRemoved"] = static () => SimIntent.Inputs,
            ["InputNameChanged"] = static () => SimIntent.Inputs,
            ["InputMuteStateChanged"] = static () => SimIntent.Inputs,
            ["InputVolumeChanged"] = static () => SimIntent.Inputs,
            ["SceneTransitionStarted"] = static () => SimIntent.Transitions,
            ["SceneTransitionEnded"] = static () => SimIntent.Transitions,
            ["SceneTransitionVideoEnded"] = static () => SimIntent.Transitions,
            ["SceneItemCreated"] = static () => SimIntent.SceneItems,
            ["SceneItemRemoved"] = static () => SimIntent.SceneItems,
            ["SceneItemListReindexed"] = static () => SimIntent.SceneItems,
            ["SceneItemEnableStateChanged"] = static () => SimIntent.SceneItems,
            ["SceneItemLockStateChanged"] = static () => SimIntent.SceneItems,
            ["SceneItemSelected"] = static () => SimIntent.SceneItems,
        };

        private readonly SimWorld _world;
        private readonly SimRequests _requests;
        private readonly PriorityQueue<Action, (TimeSpan At, long Sequence)> _actions = new();
        private readonly PriorityQueue<SimMessage, (TimeSpan At, long Sequence)> _outbound = new();
        private readonly SortedSet<TimeSpan> _deadlines = [];
        private readonly List<SimProtocolViolation> _violations = [];
        private List<EmittedEvent>? _events;
        private long _messageSequence;
        private long _actionSequence;
        private int _requestCounter;

        /// <summary>Initializes a new instance of the <see cref="SimObs"/> class.</summary>
        /// <param name="quirks">What the simulated OBS Studio does that a perfect one would not. The default is <see cref="SimQuirks.Real"/>.</param>
        /// <param name="seed">The seed of the generator of random numbers: of the UUIDs, the latencies, and everything else that is left to chance.</param>
        public SimObs(SimQuirks? quirks = null, int seed = 1)
        {
            Quirks = quirks ?? SimQuirks.Real;
            Random = new SimRandom(seed);
            Clock = new SimClock();
            _world = new SimWorld(this);
            _requests = new SimRequests(_world);
            External = new SimExternal(this, _world);
        }

        /// <summary>Gets the virtual clock.</summary>
        public SimClock Clock { get; }

        /// <summary>Gets what the simulated OBS Studio does that a perfect one would not.</summary>
        public SimQuirks Quirks { get; }

        /// <summary>Gets the changes that someone else makes: the user interface of OBS Studio, or another websocket client.</summary>
        public SimExternal External { get; }

        /// <summary>
        /// Gets the virtual time of the next thing that is scheduled: a request that has to run, a message that has to leave, the end of a lag of
        /// the list of scenes, or the end of a transition. A time that is not after <see cref="Clock"/> means that there is something to run or
        /// to take now. Null means that nothing is scheduled.
        /// </summary>
        public TimeSpan? NextDeadline
        {
            get
            {
                TimeSpan? next = null;
                if (_actions.TryPeek(out _, out var action))
                {
                    next = action.At;
                }

                if (_outbound.TryPeek(out var message, out _) && (next is null || message.SentAt < next))
                {
                    next = message.SentAt;
                }

                if (_deadlines.Count > 0 && (next is null || _deadlines.Min < next))
                {
                    next = _deadlines.Min;
                }

                return next;
            }
        }

        internal SimRandom Random { get; }

        /// <summary>Gets the number of requests, and of batches, that are never answered because a request in them hangs (fact 9).</summary>
        public int HungCount { get; private set; }

        /// <summary>
        /// A request (op 6) or a request batch (op 8) as the client sent it reaches OBS Studio at <see cref="Clock"/>. It runs at that time plus
        /// the jitter of <see cref="SimQuirks.ExecutionJitter"/>, when <see cref="Advance"/> gets there.
        /// </summary>
        /// <remarks>
        /// A message that obs-websocket does not accept (not JSON, no <c>requestId</c> or <c>requestType</c>, a field of the wrong type, an unknown
        /// operation code) does not get a response: a real OBS Studio closes the connection with a close code instead. The simulation has no
        /// connection, so it notes the close code that a real one would use (<see cref="TakeViolations"/>) and ignores the message.
        /// </remarks>
        /// <param name="messageJson">The complete JSON text of the message.</param>
        public void Receive(string messageJson)
        {
            var envelope = SimProtocol.Read(messageJson, out var violation);
            if (envelope is null)
            {
                _violations.Add(violation!);
                return;
            }

            switch (envelope.Op)
            {
                case SimOp.Request:
                    ReceiveRequest(envelope.Data);
                    break;
                case SimOp.RequestBatch:
                    ReceiveBatch(envelope.Data);
                    break;
                default:
                    _violations.Add(new SimProtocolViolation(SimCloseCode.UnknownOpCode, $"Unknown OpCode: {envelope.Op}"));
                    break;
            }
        }

        /// <summary>Builds the message of a request (op 6), calls <see cref="Receive"/> with it, and returns the ID of the request.</summary>
        /// <param name="requestType">The type of the request.</param>
        /// <param name="requestData">The request data: an object whose public properties are the fields, or null.</param>
        /// <param name="requestId">The ID of the request. A new one is made when it is null.</param>
        public string Request(string requestType, object? requestData = null, string? requestId = null)
        {
            requestId ??= "sim-" + (++_requestCounter).ToString(CultureInfo.InvariantCulture);
            var members = new List<(string Name, JsonNode? Value)> { ("requestId", requestId), ("requestType", requestType) };
            if (requestData is not null)
            {
                members.Add(("requestData", JsonSerializer.SerializeToNode(requestData)));
            }

            Receive(Serialize(Obj(("d", Obj(members.ToArray())), ("op", SimOp.Request))));
            return requestId;
        }

        /// <summary>Runs everything that is scheduled until <see cref="Clock"/> plus <paramref name="by"/>, in the order of virtual time. The clock ends at the new time.</summary>
        public void Advance(TimeSpan by)
        {
            AdvanceTo(Clock.Now + by);
        }

        /// <summary>Runs everything that is scheduled until <paramref name="time"/>, in the order of virtual time. The clock ends at that time.</summary>
        public void AdvanceTo(TimeSpan time)
        {
            if (time < Clock.Now)
            {
                throw new ArgumentOutOfRangeException(nameof(time), time, $"Virtual time is at {Clock.Now}, and cannot go back.");
            }

            while (_actions.TryPeek(out var action, out var priority) && priority.At <= time)
            {
                _actions.Dequeue();
                if (priority.At > Clock.Now)
                {
                    Clock.MoveTo(priority.At);
                }

                action();
            }

            Clock.MoveTo(time);
            _world.PruneUiList();
            _deadlines.RemoveWhere(deadline => deadline <= time);
        }

        /// <summary>
        /// Takes the messages whose time has come, in the order in which OBS Studio sends them: by the time at which they leave and then by
        /// their sequence. A message is returned once.
        /// </summary>
        public IReadOnlyList<SimMessage> TakeDue()
        {
            var due = new List<SimMessage>();
            while (_outbound.TryPeek(out var message, out _) && message.SentAt <= Clock.Now)
            {
                _outbound.Dequeue();
                due.Add(message);
            }

            return due;
        }

        /// <summary>Takes the messages that a real OBS Studio would have answered by closing the connection, and the close code of each.</summary>
        internal IReadOnlyList<SimProtocolViolation> TakeViolations()
        {
            var taken = _violations.ToArray();
            _violations.Clear();
            return taken;
        }

        /// <summary>Takes the snapshot of the state: what OBS Studio really holds, without any lag.</summary>
        public SimSnapshot Snapshot()
        {
            return _world.Snapshot();
        }

        /// <summary>Checks the rules that the state of OBS Studio follows (see <see cref="SimWorld.FindProblems"/>) and throws when one is broken.</summary>
        /// <exception cref="InvalidOperationException">A rule is broken. The message says which, and shows the state.</exception>
        public void CheckInvariants()
        {
            var problems = _world.FindProblems();
            if (_events is not null)
            {
                problems.Add("A change is still running.");
            }

            if (problems.Count > 0)
            {
                throw new InvalidOperationException(
                    "The simulated OBS Studio is in a state that OBS Studio cannot be in:"
                    + Environment.NewLine + string.Join(Environment.NewLine, problems.Select(problem => "  - " + problem))
                    + Environment.NewLine + "The state:" + Environment.NewLine + _world.Snapshot());
            }
        }

        // What the world calls to announce what happened. The index of the event tells later events to follow it by a gap.
        internal int Emit(string type, JsonObject data, int anchor, TimeSpan gap)
        {
            if (_events is null)
            {
                throw new InvalidOperationException($"{type} was emitted outside of a change.");
            }

            if (!EventIntents.TryGetValue(type, out var intent))
            {
                throw new InvalidOperationException($"The simulation does not know the event {type}.");
            }

            _events.Add(new EmittedEvent(type, data, intent(), anchor, gap));
            return _events.Count - 1;
        }

        // Something that happens later, for example the end of a transition. It is a change of its own: its events are announced alone.
        internal void Schedule(TimeSpan at, Action action)
        {
            _actions.Enqueue(() => RunChange(action), (at, _actionSequence++));
        }

        // A moment at which something that depends on time ends, such as the lag of the list of scenes.
        internal void AddDeadline(TimeSpan at)
        {
            _deadlines.Add(at);
        }

        // A change of someone else (SimExternal): it happens now, and is announced with the same latencies, but nothing is answered.
        internal T RunExternal<T>(Func<T> change)
        {
            BeginChange();
            try
            {
                return change();
            }
            catch (SimFailure failure)
            {
                throw new InvalidOperationException($"OBS Studio would not do this: {failure.Code} {failure.Comment}", failure);
            }
            catch (SimHang hang)
            {
                throw new InvalidOperationException(hang.Message, hang);
            }
            finally
            {
                EndChange(null, SimOp.RequestResponse);
            }
        }

        private static JsonObject Status(SimResult result)
        {
            return string.IsNullOrEmpty(result.Comment)
                ? Obj(("code", result.Code), ("result", result.Code == SimStatus.Success))
                : Obj(("code", result.Code), ("comment", result.Comment), ("result", result.Code == SimStatus.Success));
        }

        // The data of the response to a request (the d of op 7), or of a result of a batch.
        private static JsonObject ResponseData(string requestType, JsonNode? requestId, SimResult result, bool includeNullId)
        {
            var members = new List<(string Name, JsonNode? Value)> { ("requestStatus", Status(result)), ("requestType", requestType) };
            if (requestId is not null || includeNullId)
            {
                members.Add(("requestId", requestId?.DeepClone()));
            }

            if (result.Data is not null)
            {
                members.Add(("responseData", result.Data));
            }

            return Obj(members.ToArray());
        }

        private void ReceiveRequest(JsonObject data)
        {
            if (!data.ContainsKey("requestId"))
            {
                _violations.Add(new SimProtocolViolation(SimCloseCode.MissingDataField, "Your payload data is missing a `requestId`."));
                return;
            }

            if (!data.ContainsKey("requestType"))
            {
                _violations.Add(new SimProtocolViolation(SimCloseCode.MissingDataField, "Your payload's data is missing an `requestType`."));
                return;
            }

            if (data["requestType"]?.GetValueKind() != JsonValueKind.String)
            {
                _violations.Add(new SimProtocolViolation(SimCloseCode.InvalidDataFieldType, "Your `requestType` is not a string."));
                return;
            }

            ScheduleReceived(() => ExecuteRequest(data));
        }

        private void ReceiveBatch(JsonObject data)
        {
            if (!data.ContainsKey("requestId"))
            {
                _violations.Add(new SimProtocolViolation(SimCloseCode.MissingDataField, "Your payload data is missing a `requestId`."));
                return;
            }

            var executionType = 0;
            if (data.TryGetPropertyValue("executionType", out var executionNode) && executionNode is not null)
            {
                if (!SimJson.TryGetUnsigned(executionNode, out var requested))
                {
                    _violations.Add(new SimProtocolViolation(SimCloseCode.InvalidDataFieldType, "Your `executionType` is not a number."));
                    return;
                }

                if (requested > 2)
                {
                    _violations.Add(new SimProtocolViolation(SimCloseCode.InvalidDataFieldValue, "Your `executionType` has an invalid value."));
                    return;
                }

                executionType = (int)requested;
            }

            JsonObject? variables = null;
            if (data.TryGetPropertyValue("variables", out var variablesNode) && variablesNode is not null)
            {
                if (executionType == 2)
                {
                    _violations.Add(new SimProtocolViolation(SimCloseCode.UnsupportedFeature, "Variables are not supported in Parallel mode."));
                    return;
                }

                // ASSUMPTION: variables that are not an object are ignored (obs-websocket 5.7.6 checks the type of the whole message here,
                // so it never refuses them, and then cannot use them).
                variables = variablesNode as JsonObject;
            }

            var haltOnFailure = false;
            if (data.TryGetPropertyValue("haltOnFailure", out var haltNode) && haltNode is not null)
            {
                if (haltNode.GetValueKind() is not (JsonValueKind.True or JsonValueKind.False))
                {
                    _violations.Add(new SimProtocolViolation(SimCloseCode.InvalidDataFieldType, "Your `haltOnFailure` is not a boolean."));
                    return;
                }

                haltOnFailure = haltNode.GetValueKind() == JsonValueKind.True;
            }

            if (!data.ContainsKey("requests"))
            {
                _violations.Add(new SimProtocolViolation(SimCloseCode.MissingDataField, "Your payload data is missing a `requests`."));
                return;
            }

            if (data["requests"] is not JsonArray requests)
            {
                _violations.Add(new SimProtocolViolation(SimCloseCode.InvalidDataFieldType, "Your `requests` is not an array."));
                return;
            }

            ScheduleReceived(() => ExecuteBatch(data["requestId"], requests, executionType, haltOnFailure, variables));
        }

        // A request that was received runs after the jitter of the quirk, or at once when requests keep their order.
        private void ScheduleReceived(Action run)
        {
            var jitter = Quirks.ReorderConcurrentRequests ? Quirks.ExecutionJitter.Sample(Random) : TimeSpan.Zero;
            _actions.Enqueue(run, (Clock.Now + jitter, _actionSequence++));
        }

        private void ExecuteRequest(JsonObject data)
        {
            BeginChange();
            string? response = null;
            try
            {
                var requestType = data["requestType"]!.GetValue<string>();
                var result = RunRequest(requestType, data["requestData"] as JsonObject, variables: null, inputVariables: null);
                if (result.NoAnswer)
                {
                    HungCount++;
                }
                else
                {
                    response = Serialize(Obj(("d", ResponseData(requestType, data["requestId"], result, includeNullId: true)), ("op", SimOp.RequestResponse)));
                }
            }
            finally
            {
                EndChange(response, SimOp.RequestResponse);
            }
        }

        private SimResult RunRequest(string requestType, JsonObject? requestData, JsonObject? variables, JsonObject? inputVariables)
        {
            var request = new SimRequest(_world, requestData);
            ApplyInputVariables(request, variables, inputVariables);
            return _requests.Handle(requestType, request);
        }

        // `{"inputName": "inputNameVariable"}` is `inputName = inputNameVariable`: a field of the request data gets the value of a variable.
        // obs-websocket does nothing when there are no variables yet, or when a variable is not there (RequestBatchHandler.cpp, PreProcessVariables).
        private static void ApplyInputVariables(SimRequest request, JsonObject? variables, JsonObject? inputVariables)
        {
            if (variables is null || variables.Count == 0 || inputVariables is null || inputVariables.Count == 0)
            {
                return;
            }

            foreach (var (field, name) in inputVariables)
            {
                if (name is null || name.GetValueKind() != JsonValueKind.String)
                {
                    continue;
                }

                var variable = name.GetValue<string>();
                if (!variables.TryGetPropertyValue(variable, out var value))
                {
                    continue;
                }

                request.Data[field] = value?.DeepClone();
            }

            request.HasData = request.Data.Count > 0;
        }

        // `{"sceneItemIdVariable": "sceneItemId"}` is `sceneItemIdVariable = sceneItemId`: a variable gets the value of a field of the response.
        private static JsonObject? StoreOutputVariables(JsonObject? variables, JsonObject? outputVariables, SimResult result)
        {
            if (outputVariables is null || outputVariables.Count == 0 || result.Data is null || result.Data.Count == 0)
            {
                return variables;
            }

            foreach (var (variable, field) in outputVariables)
            {
                if (field is null || field.GetValueKind() != JsonValueKind.String)
                {
                    continue;
                }

                var name = field.GetValue<string>();
                if (!result.Data.TryGetPropertyValue(name, out var value))
                {
                    continue;
                }

                variables ??= new JsonObject();
                variables[variable] = value?.DeepClone();
            }

            return variables;
        }

        private void ExecuteBatch(JsonNode? requestId, JsonArray requests, int executionType, bool haltOnFailure, JsonObject? variables)
        {
            BeginChange();
            string? response = null;
            try
            {
                var parallel = executionType == 2;
                var labels = new List<(string Type, JsonNode? Id)>();
                var results = new List<SimResult>();
                var neverAnswered = false;
                foreach (var node in requests)
                {
                    var item = node as JsonObject ?? new JsonObject();

                    // obs-websocket replaces a requestType that is not a string by an empty one, which it then answers with 203.
                    var type = item["requestType"] is { } typeNode && typeNode.GetValueKind() == JsonValueKind.String ? typeNode.GetValue<string>() : string.Empty;
                    var result = RunRequest(
                        type,
                        item["requestData"] as JsonObject,
                        parallel ? null : variables,
                        parallel ? null : item["inputVariables"] as JsonObject);
                    if (result.NoAnswer)
                    {
                        // The thread of a request that is never answered is stuck, and so is the batch: there is no response at all.
                        neverAnswered = true;
                        if (parallel)
                        {
                            continue;
                        }

                        break;
                    }

                    if (!parallel)
                    {
                        variables = StoreOutputVariables(variables, item["outputVariables"] as JsonObject, result);
                    }

                    labels.Add((type, item["requestId"]));
                    results.Add(result);
                    if (haltOnFailure && !parallel && !result.IsSuccess)
                    {
                        break;
                    }
                }

                if (neverAnswered)
                {
                    HungCount++;
                    return;
                }

                // Fact 16 (obs-websocket#1317): the requests of a parallel batch finish in any order, and the results are put in the order in
                // which they finished, under the type and ID of the request that is at that place of the batch.
                if (parallel && Quirks.ParallelBatchMisorders)
                {
                    Random.Shuffle(results);
                }

                var list = new JsonArray();
                for (var index = 0; index < results.Count; index++)
                {
                    list.Add(ResponseData(labels[index].Type, labels[index].Id, results[index], includeNullId: false));
                }

                response = Serialize(Obj(
                    ("d", Obj(("requestId", requestId?.DeepClone()), ("results", list))),
                    ("op", SimOp.RequestBatchResponse)));
            }
            finally
            {
                EndChange(response, SimOp.RequestBatchResponse);
            }
        }

        private void RunChange(Action change)
        {
            BeginChange();
            try
            {
                change();
            }
            finally
            {
                EndChange(null, SimOp.RequestResponse);
            }
        }

        private void BeginChange()
        {
            if (_events is not null)
            {
                throw new InvalidOperationException("A change is already running.");
            }

            _world.PruneUiList();
            _events = [];
        }

        private void EndChange(string? responseJson, int responseOp)
        {
            var events = _events!;
            _events = null;
            Deliver(events, responseJson, responseOp);
        }

        // Puts the response and the events of a change in the queue of what OBS Studio sends. The events keep the order in which they were
        // emitted among themselves; the response is somewhere among them (fact 1) and the times of events of different changes interleave.
        private void Deliver(List<EmittedEvent> events, string? responseJson, int responseOp)
        {
            var now = Clock.Now;
            var count = events.Count;
            var before = responseJson is null ? count : EventsBeforeResponse(count);
            var responseAt = responseJson is null ? now : now + Quirks.ResponseLatency.Sample(Random);
            var times = new TimeSpan[count];
            for (var index = 0; index < count; index++)
            {
                times[index] = now + SampleEventLatency(events[index].Type);
            }

            var last = now;
            for (var index = 0; index < before; index++)
            {
                last = EnqueueEvent(events, times, index, last);
            }

            if (responseJson is not null)
            {
                var at = responseAt > last ? responseAt : last;
                Enqueue(new SimMessage(_messageSequence++, at, responseOp, responseJson));
                last = at;
            }

            for (var index = before; index < count; index++)
            {
                last = EnqueueEvent(events, times, index, last);
            }
        }

        private TimeSpan EnqueueEvent(List<EmittedEvent> events, TimeSpan[] times, int index, TimeSpan last)
        {
            var emitted = events[index];
            var at = times[index] > last ? times[index] : last;
            if (emitted.Anchor >= 0)
            {
                var following = times[emitted.Anchor] + emitted.Gap;
                if (following > at)
                {
                    at = following;
                }
            }

            times[index] = at;
            var json = Serialize(Obj(
                ("d", Obj(("eventData", emitted.Data), ("eventIntent", emitted.Intent), ("eventType", emitted.Type))),
                ("op", SimOp.Event)));
            Enqueue(new SimMessage(_messageSequence++, at, SimOp.Event, json));
            return at;
        }

        private void Enqueue(SimMessage message)
        {
            _outbound.Enqueue(message, (message.SentAt, message.Sequence));
        }

        private int EventsBeforeResponse(int count)
        {
            switch (Quirks.EventResponseOrder)
            {
                case SimEventResponseOrder.EventFirst:
                    return count;
                case SimEventResponseOrder.ResponseFirst:
                    return 0;
                default:
                    var before = 0;
                    while (before < count && Random.Chance(0.5))
                    {
                        before++;
                    }

                    return before;
            }
        }

        private TimeSpan SampleEventLatency(string eventType)
        {
            if (eventType == "InputSettingsChanged")
            {
                return Random.Chance(Quirks.InputSettingsChangedOutlierChance)
                    ? Quirks.InputSettingsChangedOutlier.Sample(Random)
                    : Quirks.InputSettingsChangedLatency.Sample(Random);
            }

            return Random.Chance(Quirks.EventLatencyTailChance) ? Quirks.EventLatencyTail.Sample(Random) : Quirks.EventLatency.Sample(Random);
        }

        private sealed record EmittedEvent(string Type, JsonObject Data, int Intent, int Anchor, TimeSpan Gap);
    }
}
