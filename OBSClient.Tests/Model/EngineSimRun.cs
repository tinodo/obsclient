namespace OBSStudioClient.Tests.Model
{
    using System.Globalization;
    using System.Text;
    using System.Text.Json;
    using OBSStudioClient.Classes;
    using OBSStudioClient.Messages;
    using OBSStudioClient.Model;
    using OBSStudioClient.Model.Engine;
    using OBSStudioClient.Tests.Simulation;
    using OBSStudioClient.Tests.TestSupport;

    /// <summary>What a run of <see cref="EngineSimRun"/> found, and how much it did.</summary>
    /// <param name="Problem">What is wrong, with everything that is needed to see why, or null.</param>
    /// <param name="Reads">The number of reads that the engine asked for.</param>
    /// <param name="LateResults">The number of results of reads that were handed to the engine after something else had arrived.</param>
    /// <param name="Timeouts">The number of reads that were given up.</param>
    /// <param name="Reconnects">The number of times that the connection ended and another began.</param>
    /// <param name="Messages">The number of messages of OBS Studio that arrived.</param>
    /// <param name="Digest">A digest of everything that happened: every operation, every message that arrived with its payload, every read that was sent, and the states that were compared.</param>
    /// <param name="LetGo">The number of things that the engine let go of because it had remembered them for long enough.</param>
    /// <param name="Switches">The number of times that the engine was told that OBS Studio had changed its scene collection.</param>
    /// <param name="Outdated">The number of reads that were let go of because OBS Studio changed its scene collection after they were sent.</param>
    internal sealed record EngineSimResult(string? Problem, int Reads, int LateResults, int Timeouts, int Reconnects, int Messages, string Digest, long LetGo, int Switches, int Outdated);

    /// <summary>
    /// Runs a <see cref="ModelEngine"/> against a <see cref="SimObs"/> for a random walk: someone else changes OBS Studio at random moments (its scene collection
    /// too: OBS Studio is not ready while it switches, and the model starts again from what the new collection holds), and the
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
        // The volumes travel as 32 bit numbers and as text: a difference that is not rounding is a different volume.
        private const double VolumeDbTolerance = 0.01;
        private const double VolumeMulShare = 0.001;
        private const double VolumeMulFloor = 0.000001;
        private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(10);
        private static readonly string[] AudioKinds = ["wasapi_input_capture", "wasapi_output_capture"];

        // How much of what happened a report shows (OBSCLIENT_SIM_TRACE sets it, to see more of a walk that fails).
        private static readonly int TraceLines = int.TryParse(Environment.GetEnvironmentVariable("OBSCLIENT_SIM_TRACE"), out var lines) && lines > 0 ? lines : 150;

        // What the model subscribes to (see ObsModel): OBS Studio only sends the events of these categories, and those of the transforms of the scene items and of the activity of the inputs when the model follows
        // them (ObsHighVolume.Transforms, ObsHighVolume.Activity).
        private static readonly int BaseSubscribed = SimIntent.Scenes | SimIntent.Inputs | SimIntent.SceneItems | SimIntent.Config | SimIntent.MediaInputs | SimIntent.Transitions | SimIntent.Ui;

        private readonly SimObs _sim;
        private readonly ModelEngine _engine;
        private readonly ModelEngineOptions _options;
        private readonly SimRandom _random;
        private readonly string _quirks;
        private readonly bool _collections;
        private readonly int _seed;
        private readonly List<string> _trace = [];
        private readonly List<string> _names = [];
        private readonly HashSet<Guid> _pinsReleased = [];
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
        private int _outdated;
        private ulong _digest = 14695981039346656037UL;
        public EngineSimRun(int seed, SimQuirks quirks, string quirksName, ModelEngineOptions? options = null, bool collections = true)
        {
            this._seed = seed;
            this._quirks = quirksName;
            this._options = options ?? new ModelEngineOptions();
            this._collections = collections;
            this._sim = new SimObs(quirks, seed);
            this._engine = new ModelEngine(this._options);
            this._random = new SimRandom(seed ^ 0x5EED);
            this.AddOperations();
        }

        private enum OperationKind
        {
            Change,
            Time,
            Reconnect,
        }

        public ModelEngine Engine => this._engine;

        public SimObs Sim => this._sim;

        private int Subscribed => BaseSubscribed | (this.FollowsTransforms ? SimIntent.SceneItemTransform : 0) | (this.FollowsActivity ? SimIntent.InputActive | SimIntent.InputShow : 0);

        private bool FollowsTransforms => this._options.HighVolume.HasFlag(ObsHighVolume.Transforms);

        private bool FollowsActivity => this._options.HighVolume.HasFlag(ObsHighVolume.Activity);

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
        /// <param name="collections">Whether OBS Studio changes its scene collection now and then. A walk that does not keeps what it has removed for as long as the retention says (a switch lets the model forget all of it at once).</param>
        /// <returns>What was found.</returns>
        public static EngineSimResult Execute(int seed, SimQuirks quirks, string quirksName, int steps, bool reconnects, ModelEngineOptions? options = null, int checkEvery = 100, bool collections = true)
        {
            var run = new EngineSimRun(seed, quirks, quirksName, options, collections);
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

            return new EngineSimResult(problem, run._reads, run._lateResults, run._timeouts, run._reconnects, run._messages, run._digest.ToString("x16", CultureInfo.InvariantCulture), run._engine.LetGoCount, run._engine.Epoch, run._outdated);
        }

        public void Connect()
        {
            this._session++;
            this._connected = true;
            this.Log($"connection {this._session} begins");
            this._engine.OnSessionStarted(this._session, ++this._seq, this.Tick(this.Now));
            this.DrainChanges();
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
            foreach (var line in Project(truth, this._options.HighVolume))
            {
                this.Record("truth " + line);
            }

            foreach (var line in Project(model, this._options.HighVolume))
            {
                this.Record("model " + line);
            }

            var problems = Differences(truth, model, this._options.HighVolume);
            problems.AddRange(this.Leaks(truth, model));

            // What the model holds has to be consistent with itself too: a source that OBS destroyed can be absent from every comparison below and still be held.
            problems.AddRange(this._engine.CheckConsistency().Select(problem => "inconsistent: " + problem));
            if (model.State != ModelEngineState.Synchronized)
            {
                problems.Add($"The engine is {model.State}, not Synchronized.");
            }

            if (model.Incomplete != 0 || !this._engine.IsSettled)
            {
                problems.Add($"The engine is not settled: {model.Incomplete.ToString(CultureInfo.InvariantCulture)} things are waiting.");
            }

            return problems.Count == 0 ? null : this.Describe(string.Join(Environment.NewLine, problems));
        }

        private static List<string> Differences(SimSnapshot truth, EngineSnapshot model, ObsHighVolume follows)
        {
            var truthLines = Project(truth, follows);
            var modelLines = Project(model, follows);
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
                if (held is not null && !SameVolume(input, held))
                {
                    problems.Add($"volume of \"{input.Name}\": OBS Studio {input.VolumeMul.ToString("0.######", CultureInfo.InvariantCulture)} ({input.VolumeDb.ToString("0.###", CultureInfo.InvariantCulture)} dB), model {held.VolumeMul.ToString("0.######", CultureInfo.InvariantCulture)} ({held.VolumeDb.ToString("0.###", CultureInfo.InvariantCulture)} dB)");
                }
            }

            return problems;
        }

        // A removed input that no scene item uses is not compared (see Project), but the model may hold one only when OBS Studio does. One that OBS Studio destroyed
        // and that the model still holds is a leak, unless OBS Studio destroyed it by letting go of its pin: a global audio device stays alive that way, silently
        // (no event says so, and the model learns it from the next list of inputs that it reads).
        private List<string> Leaks(SimSnapshot truth, EngineSnapshot model)
        {
            var alive = truth.Inputs.Select(input => input.Uuid).ToHashSet();
            var shown = ReferencedSources(model);
            return model.Inputs
                .Where(input => input.State == SourceState.Detached && !shown.Contains(input.Uuid) && !alive.Contains(input.Uuid) && !this._pinsReleased.Contains(input.Uuid))
                .Select(input => $"leak: the model holds the input {input.Uuid:D} \"{input.Name}\", which OBS Studio destroyed, as removed but alive.")
                .ToList();
        }

        // The decibels tell quiet levels apart (6 dB at -60 dB is a difference of 0.001 in the multiplier), and the multiplier is compared by its share, so that
        // neither one can hide a difference that the other shows.
        internal static bool SameVolume(SimInputSnapshot truth, EngineInput model)
        {
            return Math.Abs(model.VolumeDb - truth.VolumeDb) <= VolumeDbTolerance
                && Math.Abs(model.VolumeMul - truth.VolumeMul) <= Math.Max(VolumeMulFloor, VolumeMulShare * Math.Max(truth.VolumeMul, model.VolumeMul));
        }

        private static List<string> Project(SimSnapshot snapshot, ObsHighVolume follows)
        {
            var transforms = follows.HasFlag(ObsHighVolume.Transforms);
            var activity = follows.HasFlag(ObsHighVolume.Activity);
            var lines = new List<string> { "program scene " + snapshot.ProgramSceneUuid.ToString("D") };
            lines.Add(snapshot.StudioMode ? "studio mode on, preview scene " + (snapshot.PreviewSceneUuid?.ToString("D") ?? "(none)") : "studio mode off");
            lines.Add(TransitionLine(snapshot.TransitionName, snapshot.TransitionDuration, snapshot.TransitionRunning));
            lines.Add(NamesLine("profiles", snapshot.Profiles?.Current, snapshot.Profiles?.Names));
            lines.Add(NamesLine("scene collections", snapshot.SceneCollections?.Current, snapshot.SceneCollections?.Names));
            var referenced = snapshot.Scenes.Concat(snapshot.DetachedScenes).SelectMany(scene => scene.Items).Select(item => item.SourceUuid).ToHashSet();
            foreach (var scene in snapshot.Scenes)
            {
                lines.Add($"scene [{scene.Index.ToString(CultureInfo.InvariantCulture)}] {scene.Uuid:D} \"{scene.Name}\"");
                lines.AddRange(scene.Items.Select(item => ItemLine(item.Id, item.Index, item.SourceUuid, item.Enabled, item.Locked, transforms ? TransformText(JsonSerializer.Deserialize<SceneItemTransform>(item.Transform)!) : null)));
            }

            foreach (var scene in snapshot.DetachedScenes.OrderBy(scene => scene.Uuid))
            {
                lines.Add($"detached scene {scene.Uuid:D} \"{scene.Name}\"");
                lines.AddRange(scene.Items.Select(item => ItemLine(item.Id, item.Index, item.SourceUuid, item.Enabled, item.Locked, transforms ? TransformText(JsonSerializer.Deserialize<SceneItemTransform>(item.Transform)!) : null)));
            }

            foreach (var input in snapshot.Inputs.OrderBy(input => input.Uuid))
            {
                // An input that was removed and that no scene item uses (a global audio device stays alive that way) cannot be reached by
                // anybody, and nothing tells it from one that was destroyed: it is not compared.
                if (!input.Removed || referenced.Contains(input.Uuid))
                {
                    lines.Add(InputLine(input.Uuid, input.Name, input.Kind, input.HasAudio, input.Muted, input.Removed, AudioDetails(input.HasAudio, input.Balance, input.SyncOffset, input.MonitorType, input.Tracks), Canonical(input.Settings), input.MediaState, activity ? ActivityText(input.Active, input.Showing) : null));
                }
            }

            return lines;
        }

        private static List<string> Project(EngineSnapshot snapshot, ObsHighVolume follows)
        {
            var transforms = follows.HasFlag(ObsHighVolume.Transforms);
            var activity = follows.HasFlag(ObsHighVolume.Activity);
            var lines = new List<string> { "program scene " + (snapshot.ProgramSceneUuid?.ToString("D") ?? "(none)") };
            lines.Add(snapshot.StudioMode ? "studio mode on, preview scene " + (snapshot.PreviewSceneUuid?.ToString("D") ?? "(none)") : "studio mode off");
            lines.Add(TransitionLine(snapshot.Transition?.Name ?? "(none)", snapshot.Transition is { IsFixed: false } ? snapshot.TransitionDuration : null, snapshot.TransitionRunning));
            lines.Add(NamesLine("profiles", snapshot.Profiles?.Current, snapshot.Profiles?.Names));
            lines.Add(NamesLine("scene collections", snapshot.SceneCollections?.Current, snapshot.SceneCollections?.Names));
            var referenced = ReferencedSources(snapshot);
            foreach (var scene in snapshot.Scenes)
            {
                lines.Add($"scene [{scene.Index.ToString(CultureInfo.InvariantCulture)}] {scene.Uuid:D} \"{scene.Name}\"");
                lines.AddRange(scene.Items.Select(item => ItemLine(item.Id, item.Index, item.SourceUuid, item.Enabled, item.Locked, transforms ? (item.Transform is { } held ? TransformText(held) : "(no transform)") : null)));
            }

            foreach (var scene in snapshot.DetachedScenes.OrderBy(scene => scene.Uuid))
            {
                lines.Add($"detached scene {scene.Uuid:D} \"{scene.Name}\"");
                lines.AddRange(scene.Items.Select(item => ItemLine(item.Id, item.Index, item.SourceUuid, item.Enabled, item.Locked, transforms ? (item.Transform is { } held ? TransformText(held) : "(no transform)") : null)));
            }

            // The same as the other side: a removed input that no scene item uses is not compared (the model can hold it, when OBS Studio keeps it alive, and
            // Differences checks that OBS Studio does).
            foreach (var input in snapshot.Inputs.OrderBy(input => input.Uuid))
            {
                var detached = input.State == SourceState.Detached;
                if (!detached || referenced.Contains(input.Uuid))
                {
                    lines.Add(InputLine(input.Uuid, input.Name, input.Kind, input.HasAudio, input.Muted, detached, AudioDetails(input.HasAudio, input.AudioBalance, input.SyncOffset, ModelEnums.ToWire(input.MonitorType).ToString(), input.Tracks), Canonical(input.Settings?.ToString() ?? "{}"), MediaWire.Of(input.MediaState), activity ? (input.Active is { } active && input.Showing is { } showing ? ActivityText(active, showing) : "(no activity)") : null));
                }
            }

            return lines;
        }

        private static string TransitionLine(string name, int? duration, bool running)
        {
            return $"transition {name}{(duration is { } milliseconds ? " " + milliseconds.ToString(CultureInfo.InvariantCulture) + " ms" : string.Empty)}{(running ? ", running" : string.Empty)}";
        }

        // The profiles or the scene collections: the current one and all of them, in the order of OBS Studio.
        private static string NamesLine(string what, string? current, IEnumerable<string>? names)
        {
            return names is null ? $"{what} (none)" : $"{what} \"{current}\" of [{string.Join(", ", names.Select(name => $"\"{name}\""))}]";
        }

        private static HashSet<Guid> ReferencedSources(EngineSnapshot snapshot)
        {
            return snapshot.Scenes.Concat(snapshot.DetachedScenes).SelectMany(scene => scene.Items).Select(item => item.SourceUuid).ToHashSet();
        }

        private static string ItemLine(int id, int index, Guid source, bool enabled, bool locked, string? transform)
        {
            return $"    item #{id.ToString(CultureInfo.InvariantCulture)} at {index.ToString(CultureInfo.InvariantCulture)}: {source:D}{(enabled ? string.Empty : ", disabled")}{(locked ? ", locked" : string.Empty)}{(transform is null ? string.Empty : ", " + transform)}";
        }

        private static string TransformText(SceneItemTransform transform)
        {
            return TransformTexts.Of(transform);
        }

        private static string InputLine(Guid uuid, string name, string kind, bool hasAudio, bool muted, bool detached, string audioDetails, string settings, string? media, string? activity)
        {
            return $"input {uuid:D} \"{name}\" {kind}{(hasAudio ? (muted ? ", muted" : ", unmuted") : ", no audio")}{audioDetails}{(media is null ? string.Empty : ", media " + media)}{(activity is null ? string.Empty : ", " + activity)}{(settings == "{}" ? string.Empty : ", settings " + settings)}{(detached ? ", removed but alive" : string.Empty)}";
        }

        private static string ActivityText(bool active, bool showing)
        {
            return $"{(active ? "on the air" : "off the air")}, {(showing ? "shown" : "not shown")}";
        }

        private static string AudioDetails(bool hasAudio, float balance, int syncOffset, string monitorType, int tracks)
        {
            return hasAudio
                ? $", balance {balance.ToString("R", CultureInfo.InvariantCulture)}, sync offset {syncOffset.ToString(CultureInfo.InvariantCulture)} ms, {monitorType}, tracks {tracks.ToString(CultureInfo.InvariantCulture)}"
                : string.Empty;
        }

        // The settings with the fields in the same order on both sides: what is compared is what the fields are, and not how they are written.
        private static string Canonical(string settings)
        {
            return SimJson.Serialize(SimJson.Sorted(System.Text.Json.Nodes.JsonNode.Parse(settings))!);
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
            var line = $"{this.Now.TotalMilliseconds,9:0} ms  {text}";
            this.Record(line);
            this._trace.Add(line);
            if (this._trace.Count > 2 * TraceLines + 800)
            {
                this._trace.RemoveRange(0, this._trace.Count - TraceLines - 400);
            }
        }

        // Adds to the digest of the run: it covers everything that was done and everything that arrived (the report shows only the last of it, and
        // cuts what is long), so two runs that have the same digest did the same.
        private void Record(string text)
        {
            const ulong Prime = 1099511628211UL;
            foreach (var character in text)
            {
                this._digest = unchecked((this._digest ^ character) * Prime);
            }

            this._digest = unchecked((this._digest ^ '\n') * Prime);
        }

        // Runs what is due at this moment: what OBS Studio has sent, a read that is given up, a read that the engine wants to send.
        private void Process()
        {
            for (var round = 0; round < 10000; round++)
            {
                var before = this._progress;
                this._sim.AdvanceTo(this.Now);
                this.Deliver();
                this.DropOutdatedRead();
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

        // The host lets go of a read whose answer is about a scene collection that OBS Studio has left (ModelHost.PumpReads): the answer would not be applied, and the first
        // read of the new collection does not wait for it. Now and then the engine is handed what the read ended with anyway, as late as the read of a connection that ended
        // is (Reconnect): the engine does not trust the host to keep that from it, and has to ignore it.
        private void DropOutdatedRead()
        {
            if (!this._connected || this._read is not { } read || !this._engine.IsOutdated(read.Ticket))
            {
                return;
            }

            this._read = null;
            this._outdated++;
            this._progress++;
            this.Log($"read {read.Ticket.Id.ToString(CultureInfo.InvariantCulture)} is about the scene collection that OBS Studio has left: it is let go of");
            if (this._random.Chance(0.5))
            {
                this.Log($"read {read.Ticket.Id.ToString(CultureInfo.InvariantCulture)} of the scene collection that was left is handed over late");
                if (read.Results is not null)
                {
                    this._engine.OnReadCompleted(read.Ticket, read.Results, this.Tick(this.Now));
                }
                else
                {
                    this._engine.OnReadFailed(read.Ticket, this.Tick(this.Now));
                }

                this.DrainChanges();
            }
        }

        // Sends the next read of the engine, when there is one and none is under way.
        private void Pump()
        {
            if (!this._connected || this._read is not null || !this._engine.TryTakeRead(this._seq, this.Tick(this.Now), out var ticket))
            {
                return;
            }

            // A part of the model that needs more requests than the limit (the audio state of an input has six) is read alone.
            if (ticket.Requests.Count > this._options.MaxBatch && !(ticket.Requests.Count == 6 && ticket.Requests[0].Kind == ReadKind.AudioMute))
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

            var batchJson = JsonSerializer.Serialize(new { op = 8, d = new { requestId = batchId, haltOnFailure = false, executionType = 0, requests } });
            this.Record(batchJson);
            this._sim.Receive(batchJson);
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
                this.Record(message.Json);
                var at = this.Tick(message.SentAt);
                var parsed = JsonSerializer.Deserialize<ObsMessage>(message.Json);
                switch (parsed?.Data)
                {
                    case EventMessage eventMessage when ((int)eventMessage.EventIntent & Subscribed) != 0:
                        this.Log($"message {this._seq.ToString(CultureInfo.InvariantCulture)} is event {eventMessage.EventType} {Trim(eventMessage.RawEventData?.GetRawText())}");
                        this._engine.OnEvent(eventMessage, this._seq, at);
                        this.DrainChanges();
                        break;
                    case RequestBatchResponseMessage batch:
                        this.OnBatchResponse(batch, at);
                        break;
                }

                this.ApplyResultWhenDue(at);
            }

            this.ApplyResultWhenDue(this.Tick(this.Now));
        }

        private void DrainChanges()
        {
            foreach (var change in this._engine.TakeChanges())
            {
                var item = change.ItemId == 0 ? string.Empty : " item #" + change.ItemId.ToString(CultureInfo.InvariantCulture);
                var subject = change.Subject == Guid.Empty ? string.Empty : " " + Short(change.Subject);
                var text = string.IsNullOrEmpty(change.Text) ? string.Empty : $" \"{change.Text}\"";
                var values = change.OldValue is null && change.NewValue is null ? string.Empty : $" ({change.OldValue} -> {change.NewValue})";
                this.Log($"      the model: {change.Kind}{subject}{item}{text}{values}");
            }
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
                // Now and then the result that is due while OBS Studio changes its scene collection reaches the engine only after the change (the host was busy, or OBS Studio
                // answered late): by then the host has let go of the read (DropOutdatedRead), and when it has not, the engine has to ignore the result.
                read.Held ??= this._engine.IsSwitching && this._random.Chance(0.3);
                if (read.Held == true && (this._engine.IsSwitching || this._engine.IsOutdated(read.Ticket)))
                {
                    return;
                }
                if (this._seq > read.ArrivedSeq)
                {
                    this._lateResults++;
                }

                this._read = null;
                this._progress++;
                this.Log($"the result of read {read.Ticket.Id.ToString(CultureInfo.InvariantCulture)} is handed over after message {this._seq.ToString(CultureInfo.InvariantCulture)}: {string.Join(", ", read.Results.Select((response, index) => $"{response.RequestType}{(read.Ticket.Requests.Count > index && read.Ticket.Requests[index].Target != Guid.Empty ? " " + Short(read.Ticket.Requests[index].Target) : string.Empty)} = {(int)response.RequestStatus.Code}"))}");
                this._engine.OnReadCompleted(read.Ticket, read.Results, this.Tick(at));
                this.DrainChanges();
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
                this.DrainChanges();
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
                this.DrainChanges();
            }

            this._engine.OnSessionEnded(this._session, ++this._seq, this.Tick(this.Now));
            this.DrainChanges();
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

                this.DrainChanges();
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

                    // Only an input that was removed and that disappears now is destroyed without a word: the model cannot know (see Leaks).
                    if (input.Removed && this._sim.Snapshot().Inputs.All(other => other.Uuid != input.Uuid))
                    {
                        this._pinsReleased.Add(input.Uuid);
                    }
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
                // Also a source that was removed and that something keeps alive: OBS finds it by its UUID, and the new scene item keeps it alive too. Not a
                // global input that was removed: only its pin keeps it alive, nothing shows that (it is not in a list), and nobody can reach it.
                var sources = truth.Scenes.Concat(truth.DetachedScenes).Select(scene => (scene.Uuid, scene.Name)).Concat(truth.Inputs.Where(input => !input.Removed || !input.Pinned).Select(input => (input.Uuid, input.Name))).ToArray();
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
            this.Add("change the transform of an item", 4, truth => this.WithItem(truth, (scene, item) =>
            {
                var update = this.SomeTransformUpdate();
                this.Act($"change the transform of item #{item.Id} of scene \"{scene.Name}\" {Short(scene.Uuid)}", () => this._sim.External.SetItemTransform(scene.Uuid, item.Id, update));
            }));
            this.Add("change the blend mode of an item", 1, truth => this.WithItem(truth, (scene, item) =>
            {
                var mode = this.Pick(SimWorld.BlendModes);
                this.Act($"set the blend mode of item #{item.Id} of scene \"{scene.Name}\" {Short(scene.Uuid)} to {mode}", () => this._sim.External.SetItemBlendMode(scene.Uuid, item.Id, mode));
            }));
            this.Add("duplicate an item", 2, truth => this.WithItem(truth, (scene, item) =>
            {
                // Into another scene only an item of an input: a scene could be given a scene that holds it.
                var destination = !item.IsScene && this._random.Chance(0.4) ? this.Pick(truth.Scenes) : null;
                var into = destination is null ? string.Empty : $" into scene \"{destination.Name}\" {Short(destination.Uuid)}";
                this.Act($"duplicate item #{item.Id} of scene \"{scene.Name}\" {Short(scene.Uuid)}{into}", () => this._sim.External.DuplicateItem(scene.Uuid, item.Id, destination?.Uuid));
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
            this.Add("change the settings of an input", 2, truth =>
            {
                if (truth.Inputs.Count > 0)
                {
                    var input = this.Pick(truth.Inputs);
                    var fields = new System.Text.Json.Nodes.JsonObject { ["width"] = this._random.Next(1, 5) * 320 };
                    if (this._random.Chance(0.4))
                    {
                        fields["text"] = "t" + this._random.Next(0, 9).ToString(CultureInfo.InvariantCulture);
                    }

                    if (this._random.Chance(0.2))
                    {
                        fields["font"] = new System.Text.Json.Nodes.JsonObject { ["size"] = this._random.Next(10, 60) };
                    }

                    this.Act($"change the settings of input \"{input.Name}\" {Short(input.Uuid)} to {fields.ToJsonString()}", () => this._sim.External.SetSettings(input.Uuid, fields));
                }
            });
            this.Add("change the audio details of an input", 1, truth =>
            {
                var inputs = truth.Inputs.Where(input => input.HasAudio).ToArray();
                if (inputs.Length > 0)
                {
                    var input = this.Pick(inputs);
                    switch (this._random.Next(4))
                    {
                        case 0:
                            var balance = this._random.Next(0, 5) / 4f;
                            this.Act($"set the balance of input \"{input.Name}\" {Short(input.Uuid)} to {balance.ToString(CultureInfo.InvariantCulture)}", () => this._sim.External.SetBalance(input.Uuid, balance));
                            break;
                        case 1:
                            var offset = this._random.Next(-9, 20) * 50;
                            this.Act($"set the sync offset of input \"{input.Name}\" {Short(input.Uuid)} to {offset.ToString(CultureInfo.InvariantCulture)} ms", () => this._sim.External.SetSyncOffset(input.Uuid, offset));
                            break;
                        case 2:
                            var monitor = this.Pick(new[] { "OBS_MONITORING_TYPE_NONE", "OBS_MONITORING_TYPE_MONITOR_ONLY", "OBS_MONITORING_TYPE_MONITOR_AND_OUTPUT" });
                            this.Act($"set the monitor type of input \"{input.Name}\" {Short(input.Uuid)} to {monitor}", () => this._sim.External.SetMonitorType(input.Uuid, monitor));
                            break;
                        default:
                            var tracks = this._random.Next(0, 64);
                            this.Act($"set the audio tracks of input \"{input.Name}\" {Short(input.Uuid)} to {tracks.ToString(CultureInfo.InvariantCulture)}", () => this._sim.External.SetTracks(input.Uuid, tracks));
                            break;
                    }
                }
            });
            this.Add("act on the media of an input", 2, truth =>
            {
                var inputs = truth.Inputs.Where(input => input.MediaState is not null).ToArray();
                if (inputs.Length > 0)
                {
                    var input = this.Pick(inputs);
                    if (this._random.Chance(0.2))
                    {
                        this.Act($"end the media of input \"{input.Name}\" {Short(input.Uuid)}", () => this._sim.External.EndMedia(input.Uuid));
                    }
                    else
                    {
                        var action = this.Pick(new[]
                        {
                            "OBS_WEBSOCKET_MEDIA_INPUT_ACTION_PLAY",
                            "OBS_WEBSOCKET_MEDIA_INPUT_ACTION_PAUSE",
                            "OBS_WEBSOCKET_MEDIA_INPUT_ACTION_STOP",
                            "OBS_WEBSOCKET_MEDIA_INPUT_ACTION_RESTART",
                            "OBS_WEBSOCKET_MEDIA_INPUT_ACTION_NEXT",
                            "OBS_WEBSOCKET_MEDIA_INPUT_ACTION_PREVIOUS",
                        });
                        this.Act($"trigger {action} on the media of input \"{input.Name}\" {Short(input.Uuid)}", () => this._sim.External.TriggerMedia(input.Uuid, action));
                    }
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
            this.Add("switch studio mode", 2, truth =>
            {
                var enabled = !truth.StudioMode;
                this.Act($"switch studio mode {(enabled ? "on" : "off")}", () => this._sim.External.SetStudioMode(enabled));
            });
            this.Add("switch the preview scene", 3, truth =>
            {
                if (truth.StudioMode && truth.Scenes.Count > 0)
                {
                    var scene = this.Pick(truth.Scenes);
                    this.Act($"switch the preview scene to \"{scene.Name}\" {Short(scene.Uuid)}", () => this._sim.External.SwitchPreviewScene(scene.Uuid));
                }
            });
            this.Add("trigger the transition of studio mode", 3, truth =>
            {
                if (truth.StudioMode)
                {
                    this.Act("trigger the transition of studio mode", () => this._sim.External.TriggerTransition());
                }
            });
            this.Add("choose the transition", 2, _ =>
            {
                var name = this.Pick(new[] { "Cut", "Fade" });
                this.Act($"make {name} the current transition", () => this._sim.External.SetTransition(name));
            });
            this.Add("set the duration of the transitions", 2, _ =>
            {
                var milliseconds = this._random.Next(50, 2500);
                this.Act($"set the duration of the transitions to {milliseconds.ToString(CultureInfo.InvariantCulture)} ms", () => this._sim.External.SetTransitionDuration(milliseconds));
            });
            this.Add("a moment passes", 14, _ => this.RunTo(this.Now + TimeSpan.FromMilliseconds(this._random.Next(1, 120))), OperationKind.Time);
            this.Add("switch the profile", 2, _ =>
            {
                // Mostly to another one (the current one now and then: nothing happens; one that is not there now and then: OBS Studio refuses).
                var others = this._sim.Profiles.Where(name => !string.Equals(name, this._sim.CurrentProfile, StringComparison.Ordinal)).ToArray();
                var name = others.Length > 0 && this._random.Chance(0.9) ? this.Pick(others) : this._random.Chance(0.5) ? this._sim.CurrentProfile : "No such profile";
                this.Act($"switch to profile \"{name}\"", () => this._sim.External.SwitchProfile(name));
            });
            this.Add("create a profile", 1, _ =>
            {
                // A new name, and now and then the name of one that is there (OBS Studio refuses).
                var names = this._sim.Profiles;
                var name = this._random.Chance(0.1) ? this.Pick(names) : this.NewName("Profile");
                this.Act($"create profile \"{name}\"", () => this._sim.External.CreateProfile(name));
            });
            this.Add("remove a profile", 1, _ =>
            {
                var name = this._random.Chance(0.9) ? this.Pick(this._sim.Profiles) : "No such profile";
                this.Act($"remove profile \"{name}\"", () => this._sim.External.RemoveProfile(name));
            });
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
            if (!this._collections)
            {
                return;
            }

            this.Add("switch the scene collection", 3, _ =>
            {
                this.ChangeJustBefore();

                // Mostly to another one (the current one now and then: nothing happens; one that is not there now and then: OBS Studio refuses).
                var others = this._sim.Collections.Where(name => !string.Equals(name, this._sim.CurrentCollection, StringComparison.Ordinal)).ToArray();
                var name = others.Length > 0 && this._random.Chance(0.9) ? this.Pick(others) : this._random.Chance(0.5) ? this._sim.CurrentCollection : "No such collection";
                this.Act($"switch to scene collection \"{name}\"", () => this._sim.External.SwitchCollection(name));
            });
            this.Add("create a scene collection", 2, _ =>
            {
                this.ChangeJustBefore();

                // A new name, and now and then the name of one that is there (OBS Studio refuses).
                var names = this._sim.Collections;
                var name = this._random.Chance(0.1) ? this.Pick(names) : this.NewName("Collection");
                this.Act($"create scene collection \"{name}\"", () => this._sim.External.CreateCollection(name));
            });
        }

        // Often OBS Studio changes its scene collection a moment after something else was changed: then the model is reading what that change did when the switch begins, which is when
        // a read is under way that is about the collection that OBS Studio leaves.
        private void ChangeJustBefore()
        {
            if (!this._quiet && this._random.Chance(0.6))
            {
                var name = this.NewName("Scene");
                this.Act($"create scene \"{name}\"", () => this._sim.External.CreateScene(name));
                this.RunTo(this.Now + TimeSpan.FromMilliseconds(this._random.Next(1, 20)));
            }
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

        // Some members of the transform of an item: the ones that someone changes by dragging the item in the preview, now and then bounds or a crop, and always at least one.
        private SimTransformUpdate SomeTransformUpdate()
        {
            var update = new SimTransformUpdate();
            if (this._random.Chance(0.6))
            {
                update.PositionX = this._random.Next(-200, 400) / 4f;
                update.PositionY = this._random.Next(-200, 400) / 4f;
            }

            if (this._random.Chance(0.4))
            {
                update.ScaleX = this._random.Next(1, 9) / 4f;
                update.ScaleY = this._random.Next(1, 9) / 4f;
            }

            if (this._random.Chance(0.15))
            {
                update.Rotation = this._random.Next(0, 360);
            }

            if (this._random.Chance(0.2))
            {
                update.BoundsType = this.Pick(SimWorld.BoundsTypes);
                update.BoundsWidth = this._random.Next(1, 20) * 50f;
                update.BoundsHeight = this._random.Next(1, 20) * 50f;
            }

            if (this._random.Chance(0.3))
            {
                update.CropLeft = this._random.Next(0, 100);
                update.CropTop = this._random.Next(0, 100);
            }

            if (!update.HasInfo && !update.HasCrop)
            {
                update.PositionX = this._random.Next(-200, 400) / 4f;
            }

            return update;
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
                text.AppendLine(string.Join(Environment.NewLine, Project(this._engine.Snapshot(), this._options.HighVolume).Select(line => "  " + line)));
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

            // Whether the result is kept from the engine until OBS Studio has changed its scene collection (decided when the result is first due).
            public bool? Held { get; set; }
        }
    }
}
