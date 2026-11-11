namespace OBSStudioClient.Tests.Simulation
{
    using System.Globalization;
    using System.Text.Json;

    /// <summary>What a <see cref="SimWalk"/> found out about the simulation, by kind. Empty lists are what a good simulation gives.</summary>
    internal sealed class SimWalkResult
    {
        /// <summary>Gets what is wrong with the state after a step or after settling, or what made the simulation throw. The walk ends at the first.</summary>
        public List<string> Invariants { get; } = [];

        /// <summary>Gets how the answers to GetSceneList, GetInputList, GetSceneItemList and the rest differ from the snapshot, after settling.</summary>
        public List<string> Views { get; } = [];

        /// <summary>Gets the requests that were answered more than once, the answers to requests that were never made, and the requests that were not answered and do not hang.</summary>
        public List<string> Answers { get; } = [];

        /// <summary>Gets the messages that did not leave in the order of their time and sequence.</summary>
        public List<string> Order { get; } = [];

        /// <summary>Gets how the state that the events lead to differs from the real one (only for <see cref="SimQuirks.Ideal"/>).</summary>
        public List<string> Replays { get; } = [];

        /// <summary>Gets what the walk did and what it reached, by name, and how many times.</summary>
        public Dictionary<string, int> Counters { get; } = new(StringComparer.Ordinal);

        /// <summary>Gets a value indicating whether anything was found.</summary>
        public bool Failed => Invariants.Count + Views.Count + Answers.Count + Order.Count + Replays.Count > 0;
    }

    /// <summary>
    /// A random sequence of operations on a simulated OBS Studio, from a seed: requests of a client (valid ones, ones that point at things
    /// that are gone, ones that are wrong, and batches), and changes of someone else (<see cref="SimExternal"/>), with time passing in between.
    /// The same seed gives the same walk. After every step the rules of <see cref="SimObs.CheckInvariants"/> are checked, and after
    /// settling the answers of the requests are compared with the snapshot.
    /// </summary>
    internal sealed class SimWalk
    {
        private static readonly string[] Words = ["Alpha", "Bravo", "Charlie", "Delta", "Echo", "Foxtrot", "Golf", "Hotel", "India", "Juliet"];

        // Volumes in dB: the ends of the range that obs-websocket allows, and one beyond it.
        private static readonly double[] Decibels = [-100.0, -40.0, -6.02, 0.0, 6.0, 26.0, 40.0];

        // The audio balances, the sync offsets (in ms) and the monitor types that the walks set: the ends of the ranges that obs-websocket allows, and one beyond each.
        private static readonly double[] Balances = [0.0, 0.25, 0.5, 0.75, 1.0, 1.5];
        private static readonly int[] SyncOffsets = [-950, -100, 0, 150, 20000, 25000];
        private static readonly string[] MonitorTypeNames = ["OBS_MONITORING_TYPE_NONE", "OBS_MONITORING_TYPE_MONITOR_ONLY", "OBS_MONITORING_TYPE_MONITOR_AND_OUTPUT"];

        private readonly SimRandom _random;
        private readonly List<(string Name, int Weight, Action Run)> _operations = [];
        private readonly List<(int Weight, Func<(string Type, Dictionary<string, object?>? Data)> Make)> _requests = [];
        private readonly List<string> _trace = [];
        private readonly List<string> _orderProblems = [];
        private readonly List<SimMessage> _messages = [];
        private readonly Dictionary<string, string> _asked = new(StringComparer.Ordinal);
        private readonly Dictionary<Guid, (string Name, bool IsScene)> _known = [];
        private readonly HashSet<Guid> _alive = [];
        private readonly List<string> _used = [];
        private SimSnapshot _state;
        private TimeSpan _lastSentAt = TimeSpan.MinValue;
        private long _lastSequence = -1;
        private int _fresh;
        private int _batches;
        private int _steps;

        /// <summary>Initializes a new instance of the <see cref="SimWalk"/> class.</summary>
        /// <param name="seed">The seed of the walk, and of the simulation that it walks on.</param>
        /// <param name="quirks">What the simulation does that a perfect OBS Studio would not.</param>
        public SimWalk(int seed, SimQuirks quirks)
        {
            Seed = seed;
            Quirks = quirks;
            Sim = new SimObs(quirks, seed);
            _random = new SimRandom(unchecked((seed * 7919) + 104729));
            _state = Sim.Snapshot();
            Initial = _state;
            AddOperations();
        }

        /// <summary>Gets the seed.</summary>
        public int Seed { get; }

        /// <summary>Gets the quirks of the simulation.</summary>
        public SimQuirks Quirks { get; }

        /// <summary>Gets the simulation that is walked on.</summary>
        public SimObs Sim { get; }

        /// <summary>Gets the snapshot from before the first step.</summary>
        public SimSnapshot Initial { get; }

        /// <summary>Gets every message that the simulation sent, in the order in which it sent them.</summary>
        public IReadOnlyList<SimMessage> Messages => _messages;

        /// <summary>Gets what the walk did, step by step.</summary>
        public IReadOnlyList<string> Trace => _trace;

        /// <summary>Gets the IDs of the batches that were sent to run in parallel, whose results come back under the wrong type and ID when the simulation misorders them (fact 16).</summary>
        public HashSet<string> ParallelBatches { get; } = new(StringComparer.Ordinal);

        /// <summary>Gets what the walk did and what it reached, by name, and how many times.</summary>
        public Dictionary<string, int> Counters { get; } = new(StringComparer.Ordinal);

        /// <summary>Makes a walk of a number of steps, settles it, checks everything, and tells what was found.</summary>
        /// <param name="seed">The seed.</param>
        /// <param name="quirks">The quirks of the simulation.</param>
        /// <param name="steps">The number of steps.</param>
        /// <param name="keepMessages">Whether to keep the walk, with all its messages, in <paramref name="walk"/>.</param>
        /// <param name="walk">The walk, when asked for.</param>
        public static SimWalkResult Execute(int seed, SimQuirks quirks, int steps, bool keepMessages, out SimWalk? walk)
        {
            var result = new SimWalkResult();
            var made = new SimWalk(seed, quirks);
            made.Run(steps, result);
            walk = keepMessages ? made : null;
            foreach (var (name, count) in made.Counters)
            {
                result.Counters[name] = count;
            }

            return result;
        }

        /// <summary>Takes steps, settles, and checks everything. It stops at the first rule that is broken.</summary>
        /// <param name="steps">The number of steps.</param>
        /// <param name="result">Where what is found goes.</param>
        public void Run(int steps, SimWalkResult result)
        {
            try
            {
                for (var step = 0; step < steps; step++)
                {
                    Step();
                    Sim.CheckInvariants();
                }

                Settle();
                Sim.CheckInvariants();
            }
            catch (Exception exception)
            {
                result.Invariants.Add($"seed {Seed}, after step {_steps.ToString(CultureInfo.InvariantCulture)}: {exception.GetType().Name}: {exception.Message}{Environment.NewLine}The last steps:{Environment.NewLine}{string.Join(Environment.NewLine, _trace.TakeLast(12))}");
                return;
            }

            result.Order.AddRange(_orderProblems);
            CheckAnswers(result);
            if (Quirks == SimQuirks.Ideal)
            {
                result.Replays.AddRange(ReplayInStretches().Select(problem => $"seed {Seed}: {problem}"));
            }

            try
            {
                var expected = SimView.Of(Sim.Snapshot());
                var actual = SimView.Read(Sim.Snapshot().DetachedScenes, Ask);
                if (!expected.SequenceEqual(actual))
                {
                    result.Views.Add($"seed {Seed}: after settling, the answers differ from the snapshot.{Environment.NewLine}The snapshot:{Environment.NewLine}{string.Join(Environment.NewLine, expected)}{Environment.NewLine}The answers:{Environment.NewLine}{string.Join(Environment.NewLine, actual)}");
                }
            }
            catch (Exception exception) when (exception is InvalidOperationException or KeyNotFoundException or JsonException)
            {
                result.Views.Add($"seed {Seed}: {exception.Message}");
            }
        }

        /// <summary>Makes one random operation, and lets some time pass.</summary>
        public void Step()
        {
            _steps++;
            _state = Sim.Snapshot();
            Remember();

            var total = _operations.Sum(operation => operation.Weight);
            var pick = _random.Next(total);
            foreach (var (name, weight, run) in _operations)
            {
                if (pick < weight)
                {
                    Count("op " + name);
                    try
                    {
                        run();
                    }
                    catch (InvalidOperationException exception) when (exception.InnerException is SimFailure or SimHang)
                    {
                        // Someone else asked OBS Studio for something that it refuses, as the user interface or another client can.
                        Count("refused " + name);
                        _trace.Add($"  refused: {exception.Message}");
                    }

                    break;
                }

                pick -= weight;
            }

            var roll = _random.Next(100);
            var milliseconds = roll < 55 ? _random.Next(0, 10) : roll < 85 ? _random.Next(10, 150) : roll < 95 ? _random.Next(150, 1200) : _random.Next(1200, 3500);
            Sim.Advance(TimeSpan.FromMilliseconds(milliseconds));
            Take();
        }

        /// <summary>Lets time pass until nothing is scheduled any more: every request ran, every message is sent, every lag is over.</summary>
        public void Settle()
        {
            Take();
            for (var guard = 0; Sim.NextDeadline is { } next; guard++)
            {
                if (guard > 100_000)
                {
                    throw new InvalidOperationException("The simulation keeps finding something to do.");
                }

                Sim.AdvanceTo(next > Sim.Clock.Now ? next : Sim.Clock.Now);
                Take();
            }
        }

        private void Take()
        {
            foreach (var message in Sim.TakeDue())
            {
                if (message.SentAt > Sim.Clock.Now)
                {
                    _orderProblems.Add($"seed {Seed}: message {message.Sequence} leaves at {message.SentAt.TotalMilliseconds} ms, taken at {Sim.Clock.Now.TotalMilliseconds} ms.");
                }

                if (message.SentAt < _lastSentAt || (message.SentAt == _lastSentAt && message.Sequence <= _lastSequence))
                {
                    _orderProblems.Add($"seed {Seed}: message {message.Sequence} (at {message.SentAt.TotalMilliseconds} ms) comes after message {_lastSequence} (at {_lastSentAt.TotalMilliseconds} ms).");
                }

                _lastSentAt = message.SentAt;
                _lastSequence = message.Sequence;
                _messages.Add(message);
            }
        }

        private void Count(string name)
        {
            Counters[name] = Counters.GetValueOrDefault(name) + 1;
        }

        // The events of a switch of the scene collection do not hold what is in the collection that is loaded, so the events are replayed in stretches: each one from
        // what OBS Studio held when the stretch began (the start of the walk, or the loading of a collection) to what it held when it ended (the beginning of a switch,
        // or the end of the walk).
        private List<string> ReplayInStretches()
        {
            var problems = new List<string>();
            var start = Initial;
            var stretch = new List<SimMessage>();
            var switches = Sim.Switches;
            var done = 0;
            foreach (var message in _messages)
            {
                var type = message.Op == SimOp.Event ? EventTypeOf(message) : null;
                if (type == "CurrentSceneCollectionChanging")
                {
                    problems.AddRange(SimReplay.Compare(start, stretch, switches[done].Left));
                    Count("replay: stretches");
                    stretch.Clear();
                }
                else if (type == "CurrentSceneCollectionChanged")
                {
                    start = switches[done++].Loaded;
                }
                else
                {
                    stretch.Add(message);
                }
            }

            problems.AddRange(SimReplay.Compare(start, stretch, Sim.Snapshot()));
            Count("replay: stretches");
            return problems;
        }

        private static string EventTypeOf(SimMessage message)
        {
            using var document = JsonDocument.Parse(message.Json);
            return document.RootElement.GetProperty("d").GetProperty("eventType").GetString()!;
        }

        // Remembers every source that was alive, with its name, so that a request can later point at one that is gone.
        private void Remember()
        {
            _alive.Clear();
            foreach (var scene in _state.Scenes.Concat(_state.DetachedScenes))
            {
                _known[scene.Uuid] = (scene.Name, true);
                _alive.Add(scene.Uuid);
            }

            foreach (var input in _state.Inputs)
            {
                _known[input.Uuid] = (input.Name, false);
                _alive.Add(input.Uuid);
            }

            if (_state.DetachedScenes.Count > 0)
            {
                Count("state: a scene is detached");
            }

            if (_state.Inputs.Any(input => input.Removed))
            {
                Count("state: an input is removed and alive");
            }

            if (_state.Inputs.Any(input => input.Pinned))
            {
                Count("state: an input is pinned");
            }

            if (_state.Scenes.Concat(_state.DetachedScenes).Any(scene => scene.Items.Any(item => item.IsScene)))
            {
                Count("state: a scene is nested");
            }
        }

        private void CheckAnswers(SimWalkResult result)
        {
            var answered = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var message in _messages)
            {
                using var document = JsonDocument.Parse(message.Json);
                var data = document.RootElement.GetProperty("d");
                if (message.Op == SimOp.Event)
                {
                    Count("event " + data.GetProperty("eventType").GetString());
                    continue;
                }

                var id = data.GetProperty("requestId").GetString()!;
                answered[id] = answered.GetValueOrDefault(id) + 1;
                if (message.Op == SimOp.RequestResponse)
                {
                    Count($"answer {data.GetProperty("requestType").GetString()} {data.GetProperty("requestStatus").GetProperty("code").GetInt32().ToString(CultureInfo.InvariantCulture)}");
                }
                else
                {
                    Count("answer batch");
                    foreach (var item in data.GetProperty("results").EnumerateArray())
                    {
                        Count($"answer {item.GetProperty("requestType").GetString()} {item.GetProperty("requestStatus").GetProperty("code").GetInt32().ToString(CultureInfo.InvariantCulture)} (in a batch)");
                    }
                }
            }

            foreach (var (id, count) in answered)
            {
                if (!_asked.ContainsKey(id))
                {
                    result.Answers.Add($"seed {Seed}: {id} was answered, and nobody asked for it.");
                }
                else if (count > 1)
                {
                    result.Answers.Add($"seed {Seed}: {_asked[id]} {id} was answered {count} times.");
                }
            }

            var missing = _asked.Where(asked => !answered.ContainsKey(asked.Key)).Select(asked => $"{asked.Value} {asked.Key}").ToArray();
            if (missing.Length != Sim.HungCount)
            {
                result.Answers.Add($"seed {Seed}: {missing.Length} requests were not answered ({string.Join("; ", missing)}) and {Sim.HungCount} hang.");
            }

            Counters["hung"] = Sim.HungCount;
        }

        // Runs a request to its end, at the time that it is, and returns the data of its response.
        private JsonElement Ask(string type, object? data)
        {
            var id = Sim.Request(type, data, "read-" + (++_batches).ToString(CultureInfo.InvariantCulture));
            _asked[id] = type;
            Settle();
            foreach (var message in _messages.AsEnumerable().Reverse())
            {
                if (message.Op != SimOp.RequestResponse)
                {
                    continue;
                }

                using var document = JsonDocument.Parse(message.Json);
                var response = document.RootElement.GetProperty("d");
                if (response.GetProperty("requestId").GetString() != id)
                {
                    continue;
                }

                var status = response.GetProperty("requestStatus");
                if (!status.GetProperty("result").GetBoolean())
                {
                    throw new InvalidOperationException($"{type} failed with {status.GetProperty("code").GetInt32()}: {(status.TryGetProperty("comment", out var comment) ? comment.GetString() : string.Empty)}");
                }

                return response.TryGetProperty("responseData", out var responseData) ? responseData.Clone() : default;
            }

            throw new InvalidOperationException($"{type} was not answered.");
        }

        private (string Name, Guid Uuid)? FindGone(bool scene)
        {
            var gone = _known.Where(entry => entry.Value.IsScene == scene && !_alive.Contains(entry.Key)).ToArray();
            if (gone.Length == 0)
            {
                return null;
            }

            var pick = gone[_random.Next(gone.Length)];
            return (pick.Value.Name, pick.Key);
        }

        // A name for something new; now and then one that was used before, which collides with what is alive or takes the name of what is gone.
        private string Name(double reuse = 0.3)
        {
            if (_used.Count > 0 && _random.Chance(reuse))
            {
                return _used[_random.Next(_used.Count)];
            }

            var name = Words[_random.Next(Words.Length)] + "-" + (++_fresh).ToString(CultureInfo.InvariantCulture);
            _used.Add(name);
            return name;
        }

        // A scene to point at: mostly a live one, and sometimes one that is detached, gone, an input, or not there at all. Someone who sees
        // the scenes in a user interface (tidy) points at what is there nearly always.
        private (string Name, Guid Uuid) PickScene(bool tidy = false)
        {
            if (!_random.Chance(tidy ? 0.08 : 0.34))
            {
                if (_state.DetachedScenes.Count > 0 && (_state.Scenes.Count == 0 || _random.Chance(0.2)))
                {
                    var detached = _state.DetachedScenes[_random.Next(_state.DetachedScenes.Count)];
                    return (detached.Name, detached.Uuid);
                }

                if (_state.Scenes.Count > 0)
                {
                    var scene = _state.Scenes[_random.Next(_state.Scenes.Count)];
                    return (scene.Name, scene.Uuid);
                }
            }

            var roll = _random.Next(100);
            if (roll < 45 && FindGone(scene: true) is { } gone)
            {
                return gone;
            }

            if (roll < 75 && _state.Inputs.Count > 0)
            {
                var input = _state.Inputs[_random.Next(_state.Inputs.Count)];
                return (input.Name, input.Uuid);
            }

            return (Name(), _random.NextGuid());
        }

        // An input to point at; with audio when asked, if there is one.
        private (string Name, Guid Uuid) PickInput(bool audio = false, bool tidy = false)
        {
            var candidates = audio ? _state.Inputs.Where(input => input.HasAudio).ToArray() : _state.Inputs.ToArray();
            if (!_random.Chance(tidy ? 0.08 : 0.15) && candidates.Length > 0)
            {
                var input = candidates[_random.Next(candidates.Length)];
                return (input.Name, input.Uuid);
            }

            var roll = _random.Next(100);
            if (roll < 55 && FindGone(scene: false) is { } gone)
            {
                return gone;
            }

            if (roll < 80 && _state.Scenes.Count > 0)
            {
                var scene = _state.Scenes[_random.Next(_state.Scenes.Count)];
                return (scene.Name, scene.Uuid);
            }

            return (Name(), _random.NextGuid());
        }

        // A source to put in a scene: an input, or a scene that makes a nest.
        private (string Name, Guid Uuid) PickSource(bool tidy = false)
        {
            return _random.Chance(0.35) ? PickScene(tidy) : PickInput(tidy: tidy);
        }

        // The scene of an item to point at, and the ID of the item: mostly an item that is there, sometimes one that is not.
        private (string Name, Guid Uuid, int ItemId) PickItem(bool tidy = false)
        {
            var live = _state.Scenes.Where(scene => scene.Items.Count > 0).ToArray();
            var detached = _state.DetachedScenes.Where(scene => scene.Items.Count > 0).ToArray();
            SimSceneSnapshot? chosen = null;
            if (!_random.Chance(tidy ? 0.05 : 0.2))
            {
                if (detached.Length > 0 && (live.Length == 0 || _random.Chance(0.15)))
                {
                    chosen = detached[_random.Next(detached.Length)];
                }
                else if (live.Length > 0)
                {
                    chosen = live[_random.Next(live.Length)];
                }
            }

            if (chosen is null)
            {
                var (name, uuid) = PickScene(tidy);
                return (name, uuid, _random.Next(1, 6));
            }

            var id = _random.Chance(tidy ? 0.03 : 0.1) ? _random.Next(1, 30) : chosen.Items[_random.Next(chosen.Items.Count)].Id;
            return (chosen.Name, chosen.Uuid, id);
        }

        // A name, a UUID, or both, to say which one is meant (the UUID wins if both are given); or, now and then, nothing at all.
        private void Put(Dictionary<string, object?> data, string nameKey, string uuidKey, (string Name, Guid Uuid) target)
        {
            var roll = _random.Next(100);
            if (roll < 3)
            {
                return;
            }

            if (roll < 38)
            {
                data[nameKey] = target.Name;
            }
            else if (roll < 83)
            {
                data[uuidKey] = target.Uuid.ToString("D");
            }
            else
            {
                data[nameKey] = target.Name;
                data[uuidKey] = target.Uuid.ToString("D");
            }
        }

        private static Dictionary<string, object?> Data(params (string Key, object? Value)[] pairs)
        {
            var data = new Dictionary<string, object?>();
            foreach (var (key, value) in pairs)
            {
                data[key] = value;
            }

            return data;
        }

        // Now and then a request is wrong: a field is missing, of the wrong type, or there is one that nobody knows.
        private (string Type, Dictionary<string, object?>? Data) Fuzz((string Type, Dictionary<string, object?>? Data) request)
        {
            if (request.Data is null || request.Data.Count == 0 || !_random.Chance(0.04))
            {
                return request;
            }

            var key = request.Data.Keys.ElementAt(_random.Next(request.Data.Count));
            switch (_random.Next(3))
            {
                case 0:
                    request.Data.Remove(key);
                    break;
                case 1:
                    request.Data[key] = request.Data[key] is string ? (object)42 : "wrong";
                    break;
                default:
                    request.Data["bogus"] = true;
                    break;
            }

            return request;
        }

        private void Send((string Type, Dictionary<string, object?>? Data) request)
        {
            var id = Sim.Request(request.Type, request.Data);
            _asked[id] = request.Type;
            _trace.Add($"step {_steps}: request {request.Type} {(request.Data is null ? string.Empty : JsonSerializer.Serialize(request.Data))}");
        }

        private void SendBatch(List<object> requests, int executionType, bool haltOnFailure, object? variables)
        {
            var id = "walk-batch-" + (++_batches).ToString(CultureInfo.InvariantCulture);
            var data = new Dictionary<string, object?> { ["requestId"] = id, ["requests"] = requests, ["executionType"] = executionType, ["haltOnFailure"] = haltOnFailure };
            if (variables is not null)
            {
                data["variables"] = variables;
            }

            var json = JsonSerializer.Serialize(new Dictionary<string, object?> { ["op"] = SimOp.RequestBatch, ["d"] = data });
            Sim.Receive(json);
            _asked[id] = "batch";
            if (executionType == 2)
            {
                ParallelBatches.Add(id);
            }
            _trace.Add($"step {_steps}: batch (execution type {executionType}, halt {haltOnFailure}) {json}");
        }

        private (string Type, Dictionary<string, object?>? Data) Pick()
        {
            var total = _requests.Sum(request => request.Weight);
            var pick = _random.Next(total);
            foreach (var (weight, make) in _requests)
            {
                if (pick < weight)
                {
                    return Fuzz(make());
                }

                pick -= weight;
            }

            throw new InvalidOperationException("No request was picked.");
        }

        private void AddOperations()
        {
            // The requests that a client can make: the writes first (they are the ones that change the state), then the reads.
            Request(6, () => ("CreateScene", Data(("sceneName", Name()))));
            Request(4, () => Scene("RemoveScene"));
            Request(3, () => Scene("SetSceneName", ("newSceneName", Name())));
            Request(3, () => Scene("SetCurrentProgramScene"));
            Request(7, () => CreateInput());
            Request(4, () => Input("RemoveInput"));
            Request(2, () => Input("SetInputName", ("newInputName", Name())));
            Request(6, () => CreateSceneItem());
            Request(5, () => Item("RemoveSceneItem"));
            Request(3, () => Item("SetSceneItemEnabled", ("sceneItemEnabled", _random.Chance(0.5))));
            Request(2, () => Item("SetSceneItemLocked", ("sceneItemLocked", _random.Chance(0.5))));
            Request(3, () => Item("SetSceneItemIndex", ("sceneItemIndex", _random.Next(-1, 7))));
            Request(2, () => Input("SetInputMute", true, ("inputMuted", _random.Chance(0.5))));
            Request(1, () => Input("ToggleInputMute", true));
            Request(2, () => SetInputVolume());
            Request(2, () => SetInputSettings());
            Request(1, () => Input("SetInputAudioBalance", true, ("inputAudioBalance", Balances[_random.Next(Balances.Length)])));
            Request(1, () => Input("SetInputAudioSyncOffset", true, ("inputAudioSyncOffset", SyncOffsets[_random.Next(SyncOffsets.Length)])));
            Request(1, () => Input("SetInputAudioMonitorType", true, ("monitorType", _random.Chance(0.05) ? "OBS_MONITORING_TYPE_BOGUS" : MonitorTypeNames[_random.Next(MonitorTypeNames.Length)])));
            Request(1, () => SetInputAudioTracks());
            Request(1, () => Scene("GetSceneItemList"));
            Request(1, () => ("GetSceneList", null));
            Request(1, () => ("GetInputList", _random.Chance(0.3) ? Data(("inputKind", SimInputKinds.All[_random.Next(SimInputKinds.All.Count)].Id)) : null));
            Request(1, () => Item("GetSceneItemEnabled"));
            Request(1, () => Item("GetSceneItemLocked"));
            Request(1, () => Item("GetSceneItemIndex"));
            Request(1, () => Item("GetSceneItemSource"));
            Request(1, () => GetSceneItemId());
            Request(1, () => Input("GetInputMute", true));
            Request(1, () => Input("GetInputVolume", true));
            Request(1, () => Input("GetInputAudioBalance", true));
            Request(1, () => Input("GetInputAudioSyncOffset", true));
            Request(1, () => Input("GetInputAudioMonitorType", true));
            Request(1, () => Input("GetInputAudioTracks", true));
            Request(1, () => Input("GetInputSettings"));
            Request(1, () => ("GetCurrentProgramScene", null));
            Request(1, () => ("GetCurrentPreviewScene", null));
            Request(1, () => ("GetStudioModeEnabled", null));
            Request(1, () => ("GetGroupList", null));
            Request(1, () => ("GetVersion", null));
            Request(1, () => ("GetInputKindList", _random.Chance(0.5) ? Data(("unversioned", _random.Chance(0.5))) : null));
            Request(1, () => ("GetSceneCollectionList", null));
            Request(1, () => ("SetCurrentSceneCollection", Data(("sceneCollectionName", CollectionName()))));
            Request(1, () => ("CreateSceneCollection", Data(("sceneCollectionName", NewCollectionName()))));

            Operation("client request", 54, () => Send(Pick()));
            Operation("client batch", 5, AskBatch);
            Operation("client batch with variables", 2, AskVariableBatch);
            Operation("client garbage", 2, AskGarbage);
            Operation("client rename of a removed scene", 2, RenameRemovedScene);
            Operation("remove a scene that is nested", 2, RemoveNestedScene);
            Operation("make a loop of scenes", 2, MakeLoop);

            // The changes of someone else: the user interface of OBS Studio, or another client.
            Operation("external CreateScene", 4, () => Do(() => Sim.External.CreateScene(Name(0.1)), "CreateScene"));
            Operation("external RemoveScene", 3, () => Do(() => Sim.External.RemoveScene(PickScene(tidy: true).Uuid), "RemoveScene"));
            Operation("external RenameScene", 2, () => Do(() => Sim.External.RenameScene(PickScene(tidy: true).Uuid, Name(0.1)), "RenameScene"));
            Operation("external CreateInput", 5, () => Do(() => Sim.External.CreateInput(PickScene(tidy: true).Uuid, Name(0.1), KindName()), "CreateInput"));
            Operation("external CreateGlobalInput", 2, () => Do(() => Sim.External.CreateGlobalInput(Name(0.1), AudioKindName()), "CreateGlobalInput"));
            Operation("external DisableGlobalInput", 1, () => Do(() => Sim.External.DisableGlobalInput(PickPinned()), "DisableGlobalInput"));
            Operation("external RemoveInput", 3, () => Do(() => Sim.External.RemoveInput(PickInput(tidy: true).Uuid), "RemoveInput"));
            Operation("external RenameInput", 2, () => Do(() => Sim.External.RenameInput(PickInput(tidy: true).Uuid, Name(0.1)), "RenameInput"));
            Operation("external AddItem", 5, () => Do(() => Sim.External.AddItem(PickScene(tidy: true).Uuid, PickSource(tidy: true).Uuid), "AddItem"));
            Operation("external RemoveItem", 4, () => ChangeItem("RemoveItem", Sim.External.RemoveItem));
            Operation("external SetItemEnabled", 2, () => ChangeItem("SetItemEnabled", (scene, id) => Sim.External.SetItemEnabled(scene, id, _random.Chance(0.5))));
            Operation("external SetItemLocked", 1, () => ChangeItem("SetItemLocked", (scene, id) => Sim.External.SetItemLocked(scene, id, _random.Chance(0.5))));
            Operation("external SetItemIndex", 3, () => ChangeItem("SetItemIndex", (scene, id) => Sim.External.SetItemIndex(scene, id, _random.Next(-1, 7))));
            Operation("external SetMute", 1, () => Do(() => Sim.External.SetMute(PickInput(audio: true, tidy: true).Uuid, _random.Chance(0.5)), "SetMute"));
            Operation("external SetVolumeDb", 1, () => Do(() => Sim.External.SetVolumeDb(PickInput(audio: true, tidy: true).Uuid, Decibels[_random.Next(Decibels.Length)]), "SetVolumeDb"));
            Operation("external SetSettings", 2, () => Do(() => Sim.External.SetSettings(PickInput(tidy: true).Uuid, SettingsFields()), "SetSettings"));
            Operation("external SetBalance", 1, () => Do(() => Sim.External.SetBalance(PickInput(audio: true, tidy: true).Uuid, (float)Balances[_random.Next(5)]), "SetBalance"));
            Operation("external SetSyncOffset", 1, () => Do(() => Sim.External.SetSyncOffset(PickInput(audio: true, tidy: true).Uuid, SyncOffsets[_random.Next(5)]), "SetSyncOffset"));
            Operation("external SetMonitorType", 1, () => Do(() => Sim.External.SetMonitorType(PickInput(audio: true, tidy: true).Uuid, MonitorTypeNames[_random.Next(MonitorTypeNames.Length)]), "SetMonitorType"));
            Operation("external SetTracks", 1, () => Do(() => Sim.External.SetTracks(PickInput(audio: true, tidy: true).Uuid, _random.Next(64)), "SetTracks"));
            Operation("external SwitchProgramScene", 3, () => Do(() => Sim.External.SwitchProgramScene(PickScene(tidy: true).Uuid), "SwitchProgramScene"));
            Operation("external SwitchCollection", 2, () => Do(() => Sim.External.SwitchCollection(CollectionName()), "SwitchCollection"));
            Operation("external CreateCollection", 1, () => Do(() => Sim.External.CreateCollection(NewCollectionName()), "CreateCollection"));
        }

        // A scene collection to point at: mostly one that is there, and now and then one that is not.
        private string CollectionName()
        {
            var names = Sim.Collections;
            return _random.Chance(0.85) ? names[_random.Next(names.Count)] : Name(0.1);
        }

        // A name for a scene collection that is to be made: a new one, and now and then the name of one that is there.
        private string NewCollectionName()
        {
            var names = Sim.Collections;
            return _random.Chance(0.1) ? names[_random.Next(names.Count)] : Name(0.0);
        }

        private void Request(int weight, Func<(string Type, Dictionary<string, object?>? Data)> make)
        {
            _requests.Add((weight, make));
        }

        private void Operation(string name, int weight, Action run)
        {
            _operations.Add((name, weight, run));
        }

        private void Do(Action change, string name)
        {
            _trace.Add($"step {_steps}: external {name}");
            change();
        }

        private void Do<T>(Func<T> change, string name)
        {
            _trace.Add($"step {_steps}: external {name}");
            change();
        }

        private void ChangeItem(string name, Action<Guid, int> change)
        {
            var item = PickItem(tidy: true);
            Do(() => change(item.Uuid, item.ItemId), name);
        }

        private string KindName()
        {
            return _random.Chance(0.03) ? "no_such_kind" : SimInputKinds.All[_random.Next(SimInputKinds.All.Count)].Id;
        }

        private string AudioKindName()
        {
            return _random.Chance(0.5) ? "wasapi_input_capture" : "wasapi_output_capture";
        }

        private Guid PickPinned()
        {
            var pinned = _state.Inputs.Where(input => input.Pinned).ToArray();
            return pinned.Length > 0 && _random.Chance(0.85) ? pinned[_random.Next(pinned.Length)].Uuid : PickInput().Uuid;
        }

        private (string Type, Dictionary<string, object?>? Data) Scene(string type, params (string Key, object? Value)[] more)
        {
            var data = Data(more);
            Put(data, "sceneName", "sceneUuid", PickScene());
            return (type, data);
        }

        private (string Type, Dictionary<string, object?>? Data) Input(string type, params (string Key, object? Value)[] more)
        {
            return Input(type, false, more);
        }

        private (string Type, Dictionary<string, object?>? Data) Input(string type, bool audio, params (string Key, object? Value)[] more)
        {
            var data = Data(more);
            Put(data, "inputName", "inputUuid", PickInput(audio));
            return (type, data);
        }

        private (string Type, Dictionary<string, object?>? Data) Item(string type, params (string Key, object? Value)[] more)
        {
            var data = Data(more);
            var item = PickItem();
            Put(data, "sceneName", "sceneUuid", (item.Name, item.Uuid));
            data["sceneItemId"] = item.ItemId;
            return (type, data);
        }

        private (string Type, Dictionary<string, object?>? Data) CreateInput()
        {
            var data = Data(("inputName", Name()), ("inputKind", KindName()));
            Put(data, "sceneName", "sceneUuid", PickScene());
            if (_random.Chance(0.3))
            {
                data["sceneItemEnabled"] = _random.Chance(0.5);
            }

            if (_random.Chance(0.1))
            {
                data["inputSettings"] = Data(("width", 640), ("height", 360));
            }

            return ("CreateInput", data);
        }

        private (string Type, Dictionary<string, object?>? Data) CreateSceneItem()
        {
            var data = new Dictionary<string, object?>();
            Put(data, "sceneName", "sceneUuid", PickScene());
            Put(data, "sourceName", "sourceUuid", PickSource());
            if (_random.Chance(0.3))
            {
                data["sceneItemEnabled"] = _random.Chance(0.5);
            }

            return ("CreateSceneItem", data);
        }

        // Some of the settings of a text or a color source: a few fields, now and then one that is an object or a null, and from time to time an empty object.
        private System.Text.Json.Nodes.JsonObject SettingsFields()
        {
            var fields = new System.Text.Json.Nodes.JsonObject();
            if (_random.Chance(0.15))
            {
                return fields;
            }

            if (_random.Chance(0.7))
            {
                fields["width"] = _random.Next(1, 4) * 320;
            }

            if (_random.Chance(0.5))
            {
                fields["text"] = Words[_random.Next(Words.Length)];
            }

            if (_random.Chance(0.3))
            {
                fields["font"] = new System.Text.Json.Nodes.JsonObject { ["face"] = "Arial", ["size"] = _random.Next(10, 80) };
            }

            if (_random.Chance(0.2))
            {
                fields["ratio"] = 1.5;
            }

            if (_random.Chance(0.1))
            {
                fields["gone"] = null;
            }

            return fields;
        }

        private (string Type, Dictionary<string, object?>? Data) SetInputSettings()
        {
            var data = Data();
            Put(data, "inputName", "inputUuid", PickInput());
            var fields = SettingsFields();
            data["inputSettings"] = fields;
            if (_random.Chance(0.4))
            {
                data["overlay"] = _random.Chance(0.5);
            }

            return ("SetInputSettings", data);
        }

        private (string Type, Dictionary<string, object?>? Data) SetInputAudioTracks()
        {
            var data = Data();
            Put(data, "inputName", "inputUuid", PickInput(audio: true));
            var tracks = new Dictionary<string, object?>();
            for (var track = 1; track <= 6; track++)
            {
                if (_random.Chance(0.5))
                {
                    tracks[track.ToString(CultureInfo.InvariantCulture)] = _random.Chance(0.95) ? _random.Chance(0.5) : "yes";
                }
            }

            data["inputAudioTracks"] = tracks;
            return ("SetInputAudioTracks", data);
        }

        private (string Type, Dictionary<string, object?>? Data) SetInputVolume()
        {
            var data = Data();
            Put(data, "inputName", "inputUuid", PickInput(audio: true));
            switch (_random.Next(10))
            {
                case 0:
                    data["inputVolumeMul"] = 0.5;
                    data["inputVolumeDb"] = -6.0;
                    break;
                case 1:
                    break;
                case 2:
                    data["inputVolumeMul"] = 21;
                    break;
                case 3:
                case 4:
                case 5:
                    data["inputVolumeMul"] = new[] { 0.0, 0.25, 0.5, 1.0, 2.0, 20.0 }[_random.Next(6)];
                    break;
                default:
                    data["inputVolumeDb"] = new[] { -100.0, -40.0, -6.02, 0.0, 6.0, 26.0 }[_random.Next(6)];
                    break;
            }

            return ("SetInputVolume", data);
        }

        private (string Type, Dictionary<string, object?>? Data) GetSceneItemId()
        {
            var data = Data(("sourceName", _random.Chance(0.8) && _state.Inputs.Count > 0 ? _state.Inputs[_random.Next(_state.Inputs.Count)].Name : Name()));
            Put(data, "sceneName", "sceneUuid", PickScene());
            if (_random.Chance(0.4))
            {
                data["searchOffset"] = _random.Next(-1, 3);
            }

            return ("GetSceneItemId", data);
        }

        private void AskBatch()
        {
            var requests = new List<object>();
            var count = _random.Next(2, 6);
            var id = _batches + 1;
            for (var index = 0; index < count; index++)
            {
                var request = Pick();
                var entry = new Dictionary<string, object?> { ["requestType"] = request.Type };
                if (_random.Chance(0.7))
                {
                    entry["requestId"] = $"b{id.ToString(CultureInfo.InvariantCulture)}-{index.ToString(CultureInfo.InvariantCulture)}";
                }

                if (request.Data is not null)
                {
                    entry["requestData"] = request.Data;
                }

                requests.Add(entry);
            }

            var roll = _random.Next(100);
            SendBatch(requests, roll < 60 ? 0 : roll < 75 ? 1 : 2, _random.Chance(0.4), null);
        }

        // A batch whose requests hand values to each other: the item that a CreateInput makes is switched off, locked and, now and then, removed.
        private void AskVariableBatch()
        {
            var scene = PickScene();
            var target = Data(("sceneUuid", scene.Uuid.ToString("D")));
            var requests = new List<object>
            {
                new Dictionary<string, object?>
                {
                    ["requestType"] = "CreateInput",
                    ["requestData"] = Data(("sceneUuid", scene.Uuid.ToString("D")), ("inputName", Name()), ("inputKind", "color_source_v3")),
                    ["outputVariables"] = Data(("item", "sceneItemId"), ("uuid", "inputUuid")),
                },
                new Dictionary<string, object?>
                {
                    ["requestType"] = "SetSceneItemEnabled",
                    ["requestData"] = Data(("sceneUuid", scene.Uuid.ToString("D")), ("sceneItemEnabled", false)),
                    ["inputVariables"] = Data(("sceneItemId", "item")),
                },
                new Dictionary<string, object?>
                {
                    ["requestType"] = "SetSceneItemLocked",
                    ["requestData"] = Data(("sceneUuid", scene.Uuid.ToString("D")), ("sceneItemLocked", true)),
                    ["inputVariables"] = Data(("sceneItemId", "item")),
                },
                new Dictionary<string, object?>
                {
                    ["requestType"] = "GetInputSettings",
                    ["requestData"] = Data(),
                    ["inputVariables"] = Data(("inputUuid", "uuid")),
                },
            };
            if (_random.Chance(0.4))
            {
                requests.Add(new Dictionary<string, object?>
                {
                    ["requestType"] = "RemoveSceneItem",
                    ["requestData"] = target,
                    ["inputVariables"] = Data(("sceneItemId", "item")),
                });
            }

            SendBatch(requests, _random.Chance(0.7) ? 0 : 1, _random.Chance(0.5), Data(("start", 0)));
        }

        // Renames a scene that was removed and is still alive, which a real OBS Studio never answers (fact 9).
        private void RenameRemovedScene()
        {
            if (_state.DetachedScenes.Count == 0)
            {
                Send(Scene("SetSceneName", ("newSceneName", Name())));
                return;
            }

            var scene = _state.DetachedScenes[_random.Next(_state.DetachedScenes.Count)];
            Send(("SetSceneName", Data(("sceneUuid", scene.Uuid.ToString("D")), ("newSceneName", Name()))));
        }

        // Removes a scene that is an item of another scene, which then stays alive, detached from the list of scenes (fact 7).
        private void RemoveNestedScene()
        {
            var nested = _state.Scenes.Concat(_state.DetachedScenes)
                .SelectMany(scene => scene.Items)
                .Where(item => item.IsScene && _state.Scenes.Any(scene => scene.Uuid == item.SourceUuid))
                .Select(item => item.SourceUuid)
                .Distinct()
                .ToArray();
            if (nested.Length == 0)
            {
                Send(Scene("RemoveScene"));
                return;
            }

            var uuid = nested[_random.Next(nested.Length)];
            if (_random.Chance(0.5))
            {
                Send(("RemoveScene", Data(("sceneUuid", uuid.ToString("D")))));
            }
            else
            {
                Do(() => Sim.External.RemoveScene(uuid), "RemoveScene of a nested scene");
            }
        }

        // Puts a scene in a scene that is in it, which would make scenes that hold each other, and which OBS Studio refuses.
        private void MakeLoop()
        {
            var pairs = _state.Scenes.Concat(_state.DetachedScenes)
                .SelectMany(parent => parent.Items.Where(item => item.IsScene).Select(item => (Parent: parent.Uuid, Child: item.SourceUuid)))
                .ToArray();
            if (pairs.Length == 0)
            {
                Send(CreateSceneItem());
                return;
            }

            var (outer, inner) = pairs[_random.Next(pairs.Length)];
            if (_random.Chance(0.5))
            {
                Send(("CreateSceneItem", Data(("sceneUuid", inner.ToString("D")), ("sourceUuid", outer.ToString("D")))));
            }
            else
            {
                Do(() => Sim.External.AddItem(inner, outer), "AddItem that makes a loop");
            }
        }

        // A message that obs-websocket answers with a close code (there is no connection to close, so the simulation notes it), or a request
        // that it does not know.
        private void AskGarbage()
        {
            string[] broken =
            [
                "{not json",
                "[1, 2]",
                """{"op":6,"d":{"requestType":"GetVersion"}}""",
                """{"op":6,"d":{"requestId":"x"}}""",
                """{"op":42,"d":{}}""",
            ];
            var pick = _random.Next(broken.Length + 2);
            if (pick < broken.Length)
            {
                _trace.Add($"step {_steps}: garbage {broken[pick]}");
                Sim.Receive(broken[pick]);
                var violations = Sim.TakeViolations();
                Count(violations.Count == 1 ? "garbage noticed" : "garbage NOT noticed");
                if (violations.Count != 1)
                {
                    throw new InvalidOperationException($"The message {broken[pick]} gave {violations.Count} close codes.");
                }

                return;
            }

            Send(pick == broken.Length ? ("NoSuchRequest", null) : (string.Empty, null));
        }
    }
}
