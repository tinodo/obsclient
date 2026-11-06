namespace OBSStudioClient.Tests.Studio
{
    using System.Globalization;
    using System.Text;
    using System.Text.Json;
    using OBSStudioClient.Messages;
    using OBSStudioClient.Studio.Engine;
    using OBSStudioClient.Tests.Simulation;

    /// <summary>What a run of <see cref="EngineSimRun"/> found, and how much it did.</summary>
    /// <param name="Problem">What is wrong, with everything that is needed to see why, or null.</param>
    /// <param name="Reads">The number of reads that the engine asked for.</param>
    /// <param name="LateResults">The number of results of reads that were handed to the engine after something else had arrived.</param>
    /// <param name="Timeouts">The number of reads that were given up.</param>
    /// <param name="Reconnects">The number of times that the connection ended and another began.</param>
    /// <param name="Messages">The number of messages of OBS Studio that arrived.</param>
    internal sealed record EngineSimResult(string? Problem, int Reads, int LateResults, int Timeouts, int Reconnects, int Messages);

    /// <summary>
    /// Runs a <see cref="StudioEngine"/> against a <see cref="SimObs"/> for a random walk: someone else changes OBS Studio at random moments, and the
    /// engine is given what a client would be given, in the order in which a client would be given it. The reads that it asks for are sent to the
    /// simulation, and what they return is handed to the engine late, at random, after other messages. Now and then a read is given up, and the
    /// connection ends and another begins. When the walk is paused and the simulation has settled, what the engine holds has to be what the
    /// simulation holds.
    /// </summary>
    /// <remarks>
    /// Everything is deterministic: time is virtual, and the same seed gives the same run. The run that fails says the seed.
    /// </remarks>
    internal sealed class EngineSimRun
    {
        private const double VolumeTolerance = 0.002;
        private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(10);
        private static readonly string[] AudioKinds = ["wasapi_input_capture", "wasapi_output_capture"];

        // How much of what happened a report shows (OBSCLIENT_SIM_TRACE sets it, to see more of a walk that fails).
        private static readonly int TraceLines = int.TryParse(Environment.GetEnvironmentVariable("OBSCLIENT_SIM_TRACE"), out var lines) && lines > 0 ? lines : 150;

        // What the model subscribes to (see ObsStudio): OBS Studio only sends the events of these categories.
        private static readonly int Subscribed = SimIntent.Scenes | SimIntent.Inputs | SimIntent.SceneItems;

        private readonly SimObs _sim;
        private readonly StudioEngine _engine;
        private readonly StudioEngineOptions _options;
        private readonly SimRandom _random;
        private readonly string _quirks;
        private readonly int _seed;
        private readonly List<string> _trace = [];
        private readonly List<string> _names = [];
        private readonly List<Operation> _operations = [];
        private TimeSpan _clock;
        private long _seq;
        private int _session;
        private bool _connected;
        private bool _quiet;
        private Read? _read;
        private int _batches;
        private int _fresh;
        private int _step;
        private int _progress;
        private int _messages;
        private int _reads;
        private int _lateResults;
        private int _timeouts;
        private int _reconnects;

        public EngineSimRun(int seed, SimQuirks quirks, string quirksName, StudioEngineOptions? options = null)
        {
            this._seed = seed;
            this._quirks = quirksName;
            this._options = options ?? new StudioEngineOptions();
            this._sim = new SimObs(quirks, seed);
            this._engine = new StudioEngine(this._options);
            this._random = new SimRandom(seed ^ 0x5EED);
            this.AddOperations();
        }

        private enum OperationKind
        {
            Change,
            Time,
            Reconnect,
        }

        public StudioEngine Engine => this._engine;

        public SimObs Sim => this._sim;

        private TimeSpan Now => this._sim.Clock.Now;

        /// <summary>
        /// Walks, and every <paramref name="checkEvery"/> steps lets the simulation and the engine settle and compares them.
        /// </summary>
        /// <param name="seed">The seed of the run.</param>
        /// <param name="quirks">What the simulated OBS Studio does that a perfect one would not.</param>
        /// <param name="quirksName">The name of <paramref name="quirks"/>, for the report.</param>
        /// <param name="steps">The number of steps of the walk.</param>
        /// <param name="reconnects">Whether the connection ends now and then.</param>
        /// <param name="options">The options of the engine.</param>
        /// <param name="checkEvery">The number of steps between two comparisons.</param>
        /// <returns>What was found.</returns>
        public static EngineSimResult Execute(int seed, SimQuirks quirks, string quirksName, int steps, bool reconnects, StudioEngineOptions? options = null, int checkEvery = 100)
        {
            var run = new EngineSimRun(seed, quirks, quirksName, options);
            string? problem = null;
            try
            {
                run.Connect();
                for (var done = 0; done < steps && problem is null; done += checkEvery)
                {
                    run.Walk(Math.Min(checkEvery, steps - done), reconnects);
                    run.Settle();
                    problem = run.Compare();
                }
            }
            catch (Exception exception)
            {
                problem = run.Describe("An exception: " + exception);
            }

            return new EngineSimResult(problem, run._reads, run._lateResults, run._timeouts, run._reconnects, run._messages);
        }

        public void Connect()
        {
            this._session++;
            this._connected = true;
            this.Log($"connection {this._session} begins");
            this._engine.OnSessionStarted(this._session, ++this._seq, this.Tick(this.Now));
            this._progress++;
        }

        public void Walk(int steps, bool reconnects)
        {
            for (var i = 0; i < steps; i++)
            {
                this._step++;
                this.RunOneOperation(reconnects);

                // The next change is a little later, or at the same moment.
                if (this._random.Chance(0.6))
                {
                    this.RunTo(this.Now + TimeSpan.FromMilliseconds(this._random.Next(1, 40)));
                }
                else if (this._random.Chance(0.5))
                {
                    this.Process();
                }
            }
        }

        /// <summary>Lets everything run out: nothing changes in OBS Studio any more, every lag runs out, every read is answered and applied.</summary>
        public void Settle()
        {
            var start = this.Now;
            this.Process();
            while (this.NextEvent() is { } next)
            {
                if (next - start > TimeSpan.FromMinutes(10))
                {
                    throw new InvalidOperationException("The model did not settle in ten minutes of virtual time.");
                }

                this._sim.AdvanceTo(next);
                this.Process();
            }
        }

        /// <summary>Compares what the engine holds with what the simulation holds. Returns what differs, or null.</summary>
        public string? Compare()
        {
            this._sim.CheckInvariants();
            var truth = this._sim.Snapshot();
            var model = this._engine.Snapshot();
            var problems = Differences(truth, model);
            if (model.State != StudioEngineState.Synchronized)
            {
                problems.Add($"The engine is {model.State}, not Synchronized.");
            }

            if (model.Incomplete != 0 || !this._engine.IsSettled)
            {
                problems.Add($"The engine is not settled: {model.Incomplete.ToString(CultureInfo.InvariantCulture)} things are waiting.");
            }

            return problems.Count == 0 ? null : this.Describe(string.Join(Environment.NewLine, problems));
        }

        private static List<string> Differences(SimSnapshot truth, EngineSnapshot model)
        {
            var truthLines = Project(truth);
            var modelLines = Project(model);
            var problems = new List<string>();
            problems.AddRange(truthLines.Except(modelLines).Select(line => "only in OBS Studio: " + line));
            problems.AddRange(modelLines.Except(truthLines).Select(line => "only in the model:  " + line));
            if (problems.Count == 0 && !truthLines.SequenceEqual(modelLines))
            {
                problems.Add("The same things are in a different order.");
            }

            foreach (var input in truth.Inputs.Where(input => input.HasAudio))
            {
                var held = model.Inputs.FirstOrDefault(other => other.Uuid == input.Uuid);
                if (held is not null && Math.Abs(held.VolumeMul - input.VolumeMul) > VolumeTolerance)
                {
                    problems.Add($"volume of \"{input.Name}\": OBS Studio {input.VolumeMul.ToString("0.####", CultureInfo.InvariantCulture)}, model {held.VolumeMul.ToString("0.####", CultureInfo.InvariantCulture)}");
                }
            }

            return problems;
        }

        private static List<string> Project(SimSnapshot snapshot)
        {
            var lines = new List<string> { "program scene " + snapshot.ProgramSceneUuid.ToString("D") };
            var referenced = snapshot.Scenes.Concat(snapshot.DetachedScenes).SelectMany(scene => scene.Items).Select(item => item.SourceUuid).ToHashSet();
            foreach (var scene in snapshot.Scenes)
            {
                lines.Add($"scene [{scene.Index.ToString(CultureInfo.InvariantCulture)}] {scene.Uuid:D} \"{scene.Name}\"");
                lines.AddRange(scene.Items.Select(item => ItemLine(item.Id, item.Index, item.SourceUuid, item.Enabled, item.Locked)));
            }

            foreach (var scene in snapshot.DetachedScenes.OrderBy(scene => scene.Uuid))
            {
                lines.Add($"detached scene {scene.Uuid:D} \"{scene.Name}\"");
                lines.AddRange(scene.Items.Select(item => ItemLine(item.Id, item.Index, item.SourceUuid, item.Enabled, item.Locked)));
            }

            foreach (var input in snapshot.Inputs.OrderBy(input => input.Uuid))
            {
                // An input that was removed and that no scene item uses (a global audio device stays alive that way) cannot be reached by
                // anybody, and nothing tells it from one that was destroyed: it is not compared.
                if (!input.Removed || referenced.Contains(input.Uuid))
                {
                    lines.Add(InputLine(input.Uuid, input.Name, input.Kind, input.HasAudio, input.Muted, input.Removed));
                }
            }

            return lines;
        }

        private static List<string> Project(EngineSnapshot snapshot)
        {
            var lines = new List<string> { "program scene " + (snapshot.ProgramSceneUuid?.ToString("D") ?? "(none)") };
            var referenced = snapshot.Scenes.Concat(snapshot.DetachedScenes).SelectMany(scene => scene.Items).Select(item => item.SourceUuid).ToHashSet();
            foreach (var scene in snapshot.Scenes)
            {
                lines.Add($"scene [{scene.Index.ToString(CultureInfo.InvariantCulture)}] {scene.Uuid:D} \"{scene.Name}\"");
                lines.AddRange(scene.Items.Select(item => ItemLine(item.Id, item.Index, item.SourceUuid, item.Enabled, item.Locked)));
            }

            foreach (var scene in snapshot.DetachedScenes.OrderBy(scene => scene.Uuid))
            {
                lines.Add($"detached scene {scene.Uuid:D} \"{scene.Name}\"");
                lines.AddRange(scene.Items.Select(item => ItemLine(item.Id, item.Index, item.SourceUuid, item.Enabled, item.Locked)));
            }

            foreach (var input in snapshot.Inputs.OrderBy(input => input.Uuid))
            {
                var detached = input.State == SourceState.Detached;
                if (!detached || referenced.Contains(input.Uuid))
                {
                    lines.Add(InputLine(input.Uuid, input.Name, input.Kind, input.HasAudio, input.Muted, detached));
                }
            }

            return lines;
        }

        private static string ItemLine(int id, int index, Guid source, bool enabled, bool locked)
        {
            return $"    item #{id.ToString(CultureInfo.InvariantCulture)} at {index.ToString(CultureInfo.InvariantCulture)}: {source:D}{(enabled ? string.Empty : ", disabled")}{(locked ? ", locked" : string.Empty)}";
        }

        private static string InputLine(Guid uuid, string name, string kind, bool hasAudio, bool muted, bool detached)
        {
            return $"input {uuid:D} \"{name}\" {kind}{(hasAudio ? (muted ? ", muted" : ", unmuted") : ", no audio")}{(detached ? ", removed but alive" : string.Empty)}";
        }

        private static string Short(Guid uuid)
        {
            return uuid.ToString("D")[..8];
        }

        private static bool IsReferenced(SimSnapshot truth, Guid uuid)
        {
            return truth.Scenes.Concat(truth.DetachedScenes).Any(scene => scene.Items.Any(item => item.SourceUuid == uuid))
                || truth.Inputs.Any(input => input.Uuid == uuid && input.Pinned);
        }

        private TimeSpan Tick(TimeSpan time)
        {
            if (time > this._clock)
            {
                this._clock = time;
            }

            return this._clock;
        }

        private void Log(string text)
        {
            this._trace.Add($"{this.Now.TotalMilliseconds,9:0} ms  {text}");
            if (this._trace.Count > 2 * TraceLines + 800)
            {
                this._trace.RemoveRange(0, this._trace.Count - TraceLines - 400);
            }
        }

        // Runs what is due at this moment: what OBS Studio has sent, a read that is given up, a read that the engine wants to send.
        private void Process()
        {
            for (var round = 0; round < 10000; round++)
            {
                var before = this._progress;
                this._sim.AdvanceTo(this.Now);
                this.Deliver();
                this.CheckTimeout();
                this.Pump();
                if (this._progress == before)
                {
                    return;
                }
            }

            throw new InvalidOperationException("The engine and OBS Studio keep on answering each other at one moment of virtual time.");
        }

        // Lets virtual time pass to a moment, and stops at every moment on the way at which something has to happen.
        private void RunTo(TimeSpan target)
        {
            for (var guard = 0; guard < 200000; guard++)
            {
                this.Process();
                if (this.NextEvent() is not { } next || next >= target)
                {
                    if (target > this.Now)
                    {
                        this._sim.AdvanceTo(target);
                    }

                    this.Process();
                    return;
                }

                this._sim.AdvanceTo(next);
            }

            throw new InvalidOperationException("Virtual time does not pass.");
        }

        // The first moment after now at which something happens without anybody doing anything: what OBS Studio has scheduled, the wait of
        // the engine, the end of the time that a read may take, the moment that a result is handed over.
        private TimeSpan? NextEvent()
        {
            var now = this.Now;
            TimeSpan? next = null;

            void Consider(TimeSpan? time)
            {
                if (time is { } at && at > now && (next is null || at < next))
                {
                    next = at;
                }
            }

            Consider(this._sim.NextDeadline);
            if (this._connected)
            {
                Consider(this._engine.NextWake);
                if (this._read is { } read)
                {
                    Consider(read.Results is null ? read.Deadline : read.ApplyAt);
                }
            }

            return next;
        }

        // Sends the next read of the engine, when there is one and none is under way.
        private void Pump()
        {
            if (!this._connected || this._read is not null || !this._engine.TryTakeRead(this._seq, this.Tick(this.Now), out var ticket))
            {
                return;
            }

            if (ticket.Requests.Count > this._options.MaxBatch)
            {
                throw new InvalidOperationException($"A read has {ticket.Requests.Count} requests; the limit is {this._options.MaxBatch}.");
            }

            var batchId = "read-" + (++this._batches).ToString(CultureInfo.InvariantCulture);

            // Now and then a read takes longer than the host waits for it: it is given up, and its answer, if it comes, means nothing.
            var timeout = this._random.Chance(0.06) ? TimeSpan.FromMilliseconds(this._random.Next(1, 30)) : ReadTimeout;
            this._read = new Read(ticket, batchId, this.Now + timeout);
            this._reads++;
            this._progress++;
            this.Log($"read {ticket.Id.ToString(CultureInfo.InvariantCulture)} is sent after message {this._seq.ToString(CultureInfo.InvariantCulture)}: {string.Join(", ", ticket.Requests.Select(request => request.Type + (request.Target == Guid.Empty ? string.Empty : " " + Short(request.Target))))}");

            var requests = new List<Dictionary<string, object?>>();
            for (var i = 0; i < ticket.Requests.Count; i++)
            {
                var request = new Dictionary<string, object?>
                {
                    ["requestType"] = ticket.Requests[i].Type.ToString(),
                    ["requestId"] = $"{batchId}-{i.ToString(CultureInfo.InvariantCulture)}",
                };
                if (ticket.Requests[i].Data is { } data)
                {
                    request["requestData"] = data;
                }

                requests.Add(request);
            }

            this._sim.Receive(JsonSerializer.Serialize(new { op = 8, d = new { requestId = batchId, haltOnFailure = false, executionType = 0, requests } }));
        }

        // Gives the engine what OBS Studio has sent that is due, in the order in which it was sent: that is the order in which it arrives. What
        // is sent while nobody is connected is lost.
        private void Deliver()
        {
            foreach (var message in this._sim.TakeDue())
            {
                if (!this._connected)
                {
                    continue;
                }

                this._seq++;
                this._messages++;
                this._progress++;
                var at = this.Tick(message.SentAt);
                var parsed = JsonSerializer.Deserialize<ObsMessage>(message.Json);
                switch (parsed?.Data)
                {
                    case EventMessage eventMessage when ((int)eventMessage.EventIntent & Subscribed) != 0:
                        this.Log($"message {this._seq.ToString(CultureInfo.InvariantCulture)} is event {eventMessage.EventType} {Trim(eventMessage.RawEventData?.GetRawText())}");
                        this._engine.OnEvent(eventMessage, this._seq, at);
                        break;
                    case RequestBatchResponseMessage batch:
                        this.OnBatchResponse(batch, at);
                        break;
                }

                this.ApplyResultWhenDue(at);
            }

            this.ApplyResultWhenDue(this.Tick(this.Now));
        }

        private static string Trim(string? text)
        {
            return text is null ? string.Empty : text.Length <= 160 ? text : text[..160] + "...";
        }

        private void OnBatchResponse(RequestBatchResponseMessage batch, TimeSpan at)
        {
            if (this._read is not { Results: null } read || read.BatchId != batch.RequestId)
            {
                this.Log($"message {this._seq.ToString(CultureInfo.InvariantCulture)} is an answer that nobody waits for any more ({batch.RequestId})");
                return;
            }

            // The result is handed to the engine a little after it arrived: what arrives in the meantime is handed over first.
            read.Results = batch.Results;
            read.ArrivedSeq = this._seq;
            read.ApplyAfter = this._seq + (this._random.Chance(0.5) ? 0 : this._random.Next(1, 4));
            read.ApplyAt = at + TimeSpan.FromMilliseconds(this._random.Chance(0.7) ? this._random.Next(0, 6) : this._random.Next(6, 60));
            this.Log($"message {this._seq.ToString(CultureInfo.InvariantCulture)} is the answer to read {read.Ticket.Id.ToString(CultureInfo.InvariantCulture)}");
        }

        private void ApplyResultWhenDue(TimeSpan at)
        {
            if (this._read is { Results: not null } read && (this._seq >= read.ApplyAfter || at >= read.ApplyAt))
            {
                if (this._seq > read.ArrivedSeq)
                {
                    this._lateResults++;
                }

                this._read = null;
                this._progress++;
                this.Log($"the result of read {read.Ticket.Id.ToString(CultureInfo.InvariantCulture)} is handed over after message {this._seq.ToString(CultureInfo.InvariantCulture)}");
                this._engine.OnReadCompleted(read.Ticket, read.Results, this.Tick(at));
            }
        }

        private void CheckTimeout()
        {
            if (this._read is { Results: null } read && this.Now >= read.Deadline)
            {
                this._read = null;
                this._timeouts++;
                this._progress++;
                this.Log($"read {read.Ticket.Id.ToString(CultureInfo.InvariantCulture)} is given up");
                this._engine.OnReadFailed(read.Ticket, this.Tick(this.Now));
            }
        }

        // The connection ends, OBS Studio goes on without anybody who listens (and changes, but not in a way that nobody can follow), and
        // another connection begins. What belongs to the first one may reach the engine late.
        private void Reconnect()
        {
            this._reconnects++;
            this.Log("the connection ends");
            var read = this._read;
            this._read = null;
            var failedFirst = read is not null && read.Results is null && this._random.Chance(0.5);
            if (failedFirst)
            {
                this._engine.OnReadFailed(read!.Ticket, this.Tick(this.Now));
            }

            this._engine.OnSessionEnded(this._session, ++this._seq, this.Tick(this.Now));
            this._connected = false;

            // Changes that nobody sees; what OBS Studio sends meanwhile is lost with the connection.
            this._quiet = true;
            try
            {
                for (var changes = this._random.Next(0, 7); changes > 0; changes--)
                {
                    this._sim.Advance(TimeSpan.FromMilliseconds(this._random.Next(1, 400)));
                    this.RunOneOperation(reconnects: false);
                }
            }
            finally
            {
                this._quiet = false;
            }

            while (this._sim.NextDeadline is { } next)
            {
                this._sim.AdvanceTo(next > this.Now ? next : this.Now);
                this._sim.TakeDue();
            }

            this._sim.Advance(TimeSpan.FromMilliseconds(this._random.Next(0, 1500)));
            this.Connect();

            // The read of the connection that ended: its result, or its failure, reaches the engine after the new connection has begun.
            if (read is not null && !failedFirst)
            {
                this.Log($"read {read.Ticket.Id.ToString(CultureInfo.InvariantCulture)} of the connection that ended is handed over late");
                if (read.Results is not null)
                {
                    this._engine.OnReadCompleted(read.Ticket, read.Results, this.Tick(this.Now));
                }
                else
                {
                    this._engine.OnReadFailed(read.Ticket, this.Tick(this.Now));
                }
            }
        }

        private void RunOneOperation(bool reconnects)
        {
            var pool = this._operations
                .Where(operation => operation.Kind == OperationKind.Change || (!this._quiet && (operation.Kind == OperationKind.Time || reconnects)))
                .ToArray();
            var roll = this._random.Next(pool.Sum(operation => operation.Weight));
            foreach (var operation in pool)
            {
                if (roll < operation.Weight)
                {
                    try
                    {
                        operation.Run(this._sim.Snapshot());
                    }
                    catch (InvalidOperationException exception) when (exception.InnerException is SimFailure or SimHang)
                    {
                        this.Log("(OBS Studio would not do it)");
                    }

                    return;
                }

                roll -= operation.Weight;
            }
        }

        private void AddOperations()
        {
            this.Add("create scene", 5, _ =>
            {
                var name = this.NewName("Scene");
                this.Act($"create scene \"{name}\"", () => this._sim.External.CreateScene(name));
            });
            this.Add("remove scene", 3, truth =>
            {
                var scenes = truth.Scenes.Where(scene => !this._quiet || !IsReferenced(truth, scene.Uuid)).ToArray();
                if (truth.Scenes.Count > 1 && scenes.Length > 0)
                {
                    var scene = this.Pick(scenes);
                    this.Act($"remove scene \"{scene.Name}\" {Short(scene.Uuid)}", () => this._sim.External.RemoveScene(scene.Uuid));
                }
            });
            this.Add("rename scene", 2, truth =>
            {
                if (truth.Scenes.Count > 0)
                {
                    var scene = this.Pick(truth.Scenes);
                    var name = this.SomeName("Scene");
                    this.Act($"rename scene \"{scene.Name}\" {Short(scene.Uuid)} to \"{name}\"", () => this._sim.External.RenameScene(scene.Uuid, name));
                }
            });
            this.Add("create input", 6, truth =>
            {
                if (truth.Scenes.Count > 0)
                {
                    var scene = this.Pick(truth.Scenes);
                    var name = this.NewName("Input");
                    var kind = this.Pick(SimInputKinds.All).Id;
                    this.Act($"create input \"{name}\" ({kind}) in scene \"{scene.Name}\" {Short(scene.Uuid)}", () => this._sim.External.CreateInput(scene.Uuid, name, kind));
                }
            });
            this.Add("create global input", 1, _ =>
            {
                var name = this.NewName("Global");
                var kind = this.Pick(AudioKinds);
                this.Act($"create global input \"{name}\" ({kind})", () => this._sim.External.CreateGlobalInput(name, kind));
            });
            this.Add("disable global input", 1, truth =>
            {
                var pinned = truth.Inputs.Where(input => input.Pinned).ToArray();
                if (pinned.Length > 0)
                {
                    var input = this.Pick(pinned);
                    this.Act($"let go of the pin of input \"{input.Name}\" {Short(input.Uuid)}", () => this._sim.External.DisableGlobalInput(input.Uuid));
                }
            });
            this.Add("remove input", 3, truth =>
            {
                var inputs = truth.Inputs.Where(input => !input.Removed && (!this._quiet || !IsReferenced(truth, input.Uuid))).ToArray();
                if (inputs.Length > 0)
                {
                    var input = this.Pick(inputs);
                    this.Act($"remove input \"{input.Name}\" {Short(input.Uuid)}", () => this._sim.External.RemoveInput(input.Uuid));
                }
            });
            this.Add("rename input", 2, truth =>
            {
                if (truth.Inputs.Count > 0)
                {
                    var input = this.Pick(truth.Inputs);
                    var name = this.SomeName("Input");
                    this.Act($"rename input \"{input.Name}\" {Short(input.Uuid)} to \"{name}\"", () => this._sim.External.RenameInput(input.Uuid, name));
                }
            });
            this.Add("add item", 6, truth =>
            {
                var sources = truth.Scenes.Select(scene => (scene.Uuid, scene.Name)).Concat(truth.Inputs.Where(input => !input.Removed).Select(input => (input.Uuid, input.Name))).ToArray();
                if (truth.Scenes.Count > 0 && sources.Length > 0)
                {
                    var scene = this.Pick(truth.Scenes);
                    var source = this.Pick(sources);
                    this.Act($"add \"{source.Name}\" {Short(source.Uuid)} to scene \"{scene.Name}\" {Short(scene.Uuid)}", () => this._sim.External.AddItem(scene.Uuid, source.Uuid));
                }
            });
            this.Add("remove item", 4, truth => this.WithItem(truth, (scene, item) => this.Act($"remove item #{item.Id} of scene \"{scene.Name}\" {Short(scene.Uuid)}", () => this._sim.External.RemoveItem(scene.Uuid, item.Id))));
            this.Add("enable or disable item", 3, truth => this.WithItem(truth, (scene, item) =>
            {
                var enabled = !item.Enabled;
                this.Act($"{(enabled ? "enable" : "disable")} item #{item.Id} of scene \"{scene.Name}\" {Short(scene.Uuid)}", () => this._sim.External.SetItemEnabled(scene.Uuid, item.Id, enabled));
            }));
            this.Add("lock or unlock item", 2, truth => this.WithItem(truth, (scene, item) =>
            {
                var locked = !item.Locked;
                this.Act($"{(locked ? "lock" : "unlock")} item #{item.Id} of scene \"{scene.Name}\" {Short(scene.Uuid)}", () => this._sim.External.SetItemLocked(scene.Uuid, item.Id, locked));
            }));
            this.Add("move item", 4, truth => this.WithItem(truth, (scene, item) =>
            {
                var index = this._random.Next(0, scene.Items.Count);
                this.Act($"move item #{item.Id} of scene \"{scene.Name}\" {Short(scene.Uuid)} to index {index}", () => this._sim.External.SetItemIndex(scene.Uuid, item.Id, index));
            }));
            this.Add("mute or unmute input", 2, truth =>
            {
                var inputs = truth.Inputs.Where(input => input.HasAudio).ToArray();
                if (inputs.Length > 0)
                {
                    var input = this.Pick(inputs);
                    var muted = !input.Muted;
                    this.Act($"{(muted ? "mute" : "unmute")} input \"{input.Name}\" {Short(input.Uuid)}", () => this._sim.External.SetMute(input.Uuid, muted));
                }
            });
            this.Add("set the volume of an input", 2, truth =>
            {
                var inputs = truth.Inputs.Where(input => input.HasAudio).ToArray();
                if (inputs.Length > 0)
                {
                    var input = this.Pick(inputs);
                    var db = this._random.Next(-60, 12);
                    this.Act($"set the volume of input \"{input.Name}\" {Short(input.Uuid)} to {db} dB", () => this._sim.External.SetVolumeDb(input.Uuid, db));
                }
            });
            this.Add("switch the program scene", 3, truth =>
            {
                if (truth.Scenes.Count > 0)
                {
                    var scene = this.Pick(truth.Scenes);
                    this.Act($"switch the program scene to \"{scene.Name}\" {Short(scene.Uuid)}", () => this._sim.External.SwitchProgramScene(scene.Uuid));
                }
            });
            this.Add("a moment passes", 14, _ => this.RunTo(this.Now + TimeSpan.FromMilliseconds(this._random.Next(1, 120))), OperationKind.Time);
            this.Add("a while passes", 3, _ => this.RunTo(this.Now + TimeSpan.FromMilliseconds(this._random.Next(300, 3000))), OperationKind.Time);
            this.Add("a long while passes", 1, _ => this.RunTo(this.Now + TimeSpan.FromMilliseconds(this._random.Next(5000, 15000))), OperationKind.Time);
            this.Add("the connection ends", 1, truth =>
            {
                // A new connection reads everything again and cannot tell that OBS Studio kept alive what it removed (there is no way to tell it
                // from what is not removed): the model starts from what is in the lists. So the connection ends when nothing is kept alive.
                if (truth.DetachedScenes.Count == 0 && !truth.Inputs.Any(input => input.Removed))
                {
                    this.Reconnect();
                }
            }, OperationKind.Reconnect);
        }

        private void Add(string name, int weight, Action<SimSnapshot> run, OperationKind kind = OperationKind.Change)
        {
            this._operations.Add(new Operation(name, weight, kind, run));
        }

        private void Act(string description, Action change)
        {
            this.Log("someone else: " + description);
            change();
        }

        private void WithItem(SimSnapshot truth, Action<SimSceneSnapshot, SimItemSnapshot> act)
        {
            // Only in a scene that is in the list. obs-websocket 5.7.6 announces nothing that happens to a scene that was removed and is kept
            // alive (it only announces what happens in the main canvas, and such a scene has none), so nobody can follow those items.
            var scenes = truth.Scenes.Where(scene => scene.Items.Count > 0).ToArray();
            if (scenes.Length > 0)
            {
                var scene = this.Pick(scenes);
                act(scene, this.Pick(scene.Items));
            }
        }

        private T Pick<T>(IReadOnlyList<T> items)
        {
            return items[this._random.Next(items.Count)];
        }

        private string NewName(string prefix)
        {
            var name = $"{prefix} {(++this._fresh).ToString(CultureInfo.InvariantCulture)}";
            this._names.Add(name);
            return name;
        }

        // A name that is new, or now and then one that was used before: it is free again, or it is not.
        private string SomeName(string prefix)
        {
            return this._names.Count > 0 && this._random.Chance(0.25) ? this.Pick(this._names) : this.NewName(prefix);
        }

        private string Describe(string problem)
        {
            var text = new StringBuilder();
            text.AppendLine($"Seed {this._seed.ToString(CultureInfo.InvariantCulture)}, quirks {this._quirks}, MaxBatch {this._options.MaxBatch.ToString(CultureInfo.InvariantCulture)}, step {this._step.ToString(CultureInfo.InvariantCulture)}, virtual time {this.Now}.");
            text.AppendLine($"{this._messages.ToString(CultureInfo.InvariantCulture)} messages, {this._reads.ToString(CultureInfo.InvariantCulture)} reads, {this._lateResults.ToString(CultureInfo.InvariantCulture)} late results, {this._timeouts.ToString(CultureInfo.InvariantCulture)} given up, {this._reconnects.ToString(CultureInfo.InvariantCulture)} reconnects.");
            text.AppendLine(problem.TrimEnd());
            try
            {
                text.AppendLine("OBS Studio holds:");
                text.AppendLine(this._sim.Snapshot().ToString());
                text.AppendLine("The model holds:");
                text.AppendLine(string.Join(Environment.NewLine, Project(this._engine.Snapshot()).Select(line => "  " + line)));
            }
            catch (Exception exception)
            {
                text.AppendLine("(the state could not be shown: " + exception.Message + ")");
            }

            text.AppendLine("The last of what happened:");
            foreach (var line in this._trace.TakeLast(TraceLines))
            {
                text.AppendLine("  " + line);
            }

            return text.ToString();
        }

        private sealed record Operation(string Name, int Weight, OperationKind Kind, Action<SimSnapshot> Run);

        private sealed class Read
        {
            public Read(ReadTicket ticket, string batchId, TimeSpan deadline)
            {
                this.Ticket = ticket;
                this.BatchId = batchId;
                this.Deadline = deadline;
            }

            public ReadTicket Ticket { get; }

            public string BatchId { get; }

            public TimeSpan Deadline { get; }

            public RequestResponseMessage[]? Results { get; set; }

            public long ArrivedSeq { get; set; }

            public long ApplyAfter { get; set; }

            public TimeSpan ApplyAt { get; set; }
        }
    }
}
