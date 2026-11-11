namespace OBSStudioClient.Model.Engine
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Messages;
    using OBSStudioClient.Responses;

    /// <summary>
    /// What a read is for.
    /// </summary>
    internal enum ReadKind
    {
        /// <summary>GetSceneList.</summary>
        Scenes,

        /// <summary>GetInputList.</summary>
        Inputs,

        /// <summary>GetCurrentProgramScene.</summary>
        Program,

        /// <summary>GetStudioModeEnabled.</summary>
        StudioMode,

        /// <summary>GetSceneItemList, or GetGroupSceneItemList for a group.</summary>
        Items,

        /// <summary>GetInputMute: the first request of the audio state of an input, which is read with six requests: mute, volume, balance, sync offset, monitor type and tracks.</summary>
        AudioMute,

        /// <summary>GetInputVolume (always right after GetInputMute of the same input).</summary>
        AudioVolume,

        /// <summary>GetInputAudioBalance (always the third request of the audio state).</summary>
        AudioBalance,

        /// <summary>GetInputAudioSyncOffset (always the fourth request of the audio state).</summary>
        AudioSyncOffset,

        /// <summary>GetInputAudioMonitorType (always the fifth request of the audio state).</summary>
        AudioMonitorType,

        /// <summary>GetInputAudioTracks (always the sixth request of the audio state).</summary>
        AudioTracks,

        /// <summary>GetInputSettings.</summary>
        InputSettings,

        /// <summary>GetMediaInputStatus.</summary>
        MediaStatus,

        /// <summary>GetStreamStatus.</summary>
        Stream,

        /// <summary>GetRecordStatus.</summary>
        Record,

        /// <summary>GetVirtualCamStatus.</summary>
        VirtualCamera,

        /// <summary>GetReplayBufferStatus.</summary>
        ReplayBuffer,

        /// <summary>GetSourceFilterList, for a scene or an input.</summary>
        Filters,
    }

    /// <summary>
    /// One request of a read.
    /// </summary>
    /// <param name="Kind">What the request is for.</param>
    /// <param name="Type">The type of request.</param>
    /// <param name="Data">The request data.</param>
    /// <param name="Target">The scene or input that the request is about, or <see cref="Guid.Empty"/>.</param>
    internal sealed record ReadRequest(ReadKind Kind, RequestType Type, object? Data, Guid Target);

    /// <summary>
    /// A part of the model that the application asks to have read again: a change that it made was not shown by the events in time.
    /// </summary>
    /// <param name="Kind">What is read: the list of scenes, the program scene, the scene items of a scene, the audio state of an input, and so on.</param>
    /// <param name="Target">The scene or input that it is about, or <see cref="Guid.Empty"/> for the lists, the program scene and studio mode.</param>
    internal readonly record struct RefreshTarget(ReadKind Kind, Guid Target = default);

    /// <summary>
    /// The requests that the engine wants to have sent, as one serial batch, and what it needs to know about the moment they were sent.
    /// </summary>
    internal sealed class ReadTicket
    {
        public ReadTicket(int id, int sessionId, int epoch, long sentSeq, TimeSpan sentAt, IReadOnlyList<ReadRequest> requests)
        {
            this.Id = id;
            this.SessionId = sessionId;
            this.Epoch = epoch;
            this.SentSeq = sentSeq;
            this.SentAt = sentAt;
            this.Requests = requests;
        }

        /// <summary>Gets the number of the ticket.</summary>
        public int Id { get; }

        /// <summary>Gets the connection that the read belongs to: its result means nothing to another one.</summary>
        public int SessionId { get; }

        /// <summary>
        /// Gets the number of the scene collection that the read belongs to (it goes up each time that OBS Studio has changed its scene collection): what
        /// it says is about the collection that OBS Studio had when it was sent, and means nothing to the model of the one that it has now.
        /// </summary>
        public int Epoch { get; }

        /// <summary>Gets the number of the last message that had arrived when the requests were sent.</summary>
        public long SentSeq { get; }

        /// <summary>Gets the time at which the requests were sent.</summary>
        public TimeSpan SentAt { get; }

        /// <summary>Gets the requests.</summary>
        public IReadOnlyList<ReadRequest> Requests { get; }
    }

    /// <summary>
    /// The state of the connection of the engine.
    /// </summary>
    internal enum ModelEngineState
    {
        /// <summary>There is no connection (or it ended): what the model holds is the last that was known.</summary>
        Disconnected,

        /// <summary>
        /// The model is being read: for the first time on this connection, or again because OBS Studio is changing its scene collection or has changed it
        /// (what the model holds is then the collection that OBS Studio is leaving, until it has loaded the other).
        /// </summary>
        Synchronizing,

        /// <summary>Everything was read, and nothing is waiting for a read.</summary>
        Synchronized,
    }

    /// <summary>
    /// The settings of the engine.
    /// </summary>
    internal sealed class ModelEngineOptions
    {
        /// <summary>
        /// Gets or sets how long a scene that an event announced may be missing from the list of scenes before the model concludes that it
        /// does not exist (the list of OBS lags behind its events: about 2 seconds were measured).
        /// </summary>
        public TimeSpan CreationGrace { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Gets or sets the number of requests in one read. It cannot be less than 2. A part of the model that is read with more requests than that (the audio state of an input has
        /// six) is read alone, in a read of its own.
        /// </summary>
        public int MaxBatch { get; set; } = 50;

        /// <summary>Gets or sets how long the engine waits before it checks the list of inputs after an input lost its last scene item.</summary>
        public TimeSpan InputCheckDelay { get; set; } = TimeSpan.FromMilliseconds(200);

        /// <summary>Gets or sets the wait before the first repeat of a read.</summary>
        public TimeSpan MinBackoff { get; set; } = TimeSpan.FromMilliseconds(250);

        /// <summary>Gets or sets the longest wait before a repeat of a read.</summary>
        public TimeSpan MaxBackoff { get; set; } = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Gets or sets how long the engine remembers what it was told was removed or destroyed (and the scene items that a read found gone before their event
        /// came): the messages that it guards against (a read that was sent before the removal, the second copy of a creation event, an event that is late) arrive
        /// within the time that a read may take and the lag of OBS, which is seconds. Without a limit the memory would grow for as long as a connection lasts.
        /// A source is remembered until a list of its kind shows that OBS is done with it (OBS goes on listing an input that it keeps alive for something that
        /// nothing shows), and then for this time; one that no list confirmed in this time is asked about with a read of the list. A removed input that nothing
        /// that the model knows uses is destroyed when the time for the scene items that a read found gone, and that it was used by, is up (counted from the
        /// read, also when an event told about them first: the events that were on their way when the read was taken can still follow). The time that the engine is
        /// given with a message must be the time at which the message arrived, not the time at which it is handled: when a host is late with the messages that wait
        /// (it was held, or the computer slept with the connection open), the time that passes while they wait must not count, or what the engine remembers could be
        /// let go of before the events that it remembers it for are handled.
        /// </summary>
        public TimeSpan TombstoneRetention { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>Gets or sets how often the engine looks for what it need not remember any more: it does so when it is given something, at most this often.</summary>
        public TimeSpan TombstoneCleanupInterval { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets how long an output may be starting or stopping before the engine reads it, to see whether the step is over. OBS Studio does not always say that a start failed (a
        /// recording that cannot begin shows an error in its own window and sends no event after the first), so a step that lasts this long is checked: a start that is not running then is over.
        /// </summary>
        public TimeSpan OutputStepTimeout { get; set; } = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// The model of OBS Studio, without any input or output: a state machine that is told what arrived (events, the results of reads, the
    /// end of a connection) and what time it is, and that says what it wants to read. It is single threaded, and not thread safe.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Everything that arrives has a number, in the order of arrival (see <c>ObsClient.LastIngress</c>). A read remembers the number of the last
    /// message that had arrived when it was sent. The result of a read is applied to a value only when no event told that value something
    /// after the read was sent: an event beats an older read, whatever the order in which they are processed.
    /// </para>
    /// <para>
    /// OBS counts references. An input or scene that OBS removes while a scene item still uses it stays alive (state Detached), together with
    /// its scene items, until the last scene item goes. The model follows that, and hides nothing.
    /// </para>
    /// </remarks>
    internal sealed partial class ModelEngine
    {
        private readonly ModelEngineOptions _options;
        private readonly Dictionary<Guid, SceneNode> _scenes = new();
        private readonly Dictionary<Guid, InputNode> _inputs = new();
        private readonly Dictionary<Guid, HashSet<ItemKey>> _refs = new();

        // What OBS destroyed, and what was removed before the model knew it. Both guard against the messages that are older than the removal: a
        // read that was sent before it, the second copy of a creation event, a list that still has the source. They are for one connection: another one
        // reads everything again, and a source that was removed can be there again (the scene collection that had it was loaded again). Within one they are
        // remembered until a list shows that OBS is done with the source, and for a while after that (see SourceTombstones): OBS goes on listing an input that
        // it removed and keeps alive for something that nothing shows (a global audio input), and the model must not take it for a new input.
        private readonly SourceTombstones _gone = new();
        private readonly TimedSet<ItemKey> _goneItems = new();
        private readonly SourceTombstones _removedUnknown = new();

        // The scene items that a read found gone, whose removal the events have not told yet, and the sources that they used. A read is ahead of the events:
        // such a scene item existed as far as the events go, so while it is here a source that nothing else is known to use may still be used (see
        // HeldByRead). The event that removes the scene item takes it away, and so does the time (the event does not always come).
        private readonly ReadReleasedItems _readReleased = new();
        private TimeSpan _lastCleanup;
        private long _letGo;

        // The sources that were removed while the model did not know their scene items yet: whether OBS destroyed them is not known.
        private readonly HashSet<Guid> _unresolved = new();
        private readonly List<ModelChange> _changes = new();
        private readonly ScopeState _scenesScope = new();
        private readonly ScopeState _inputsScope = new();
        private readonly ScopeState _programScope = new();
        private readonly ScopeState _studioScope = new();

        // The outputs (stream, recording, virtual camera, replay buffer), in the order of ObsOutputKind. They do not belong to a scene collection.
        private readonly OutputNode[] _outputs = [new(ObsOutputKind.Stream), new(ObsOutputKind.Record), new(ObsOutputKind.VirtualCamera), new(ObsOutputKind.ReplayBuffer)];
        private Cell<Guid?> _programUuid = new(null, 0);
        private Cell<bool> _studioMode = new(false, 0);
        private Cell<bool> _exiting = new(false, 0);
        private ModelEngineState _state = ModelEngineState.Disconnected;
        private TimeSpan _now;
        private int _nextTicket;
        private long _nextFilterId;
        private int _sessionId;
        private int _epoch;
        private bool _switching;
        private bool _scenesRead;
        private bool _inputsRead;
        private bool _programRead;
        private bool _studioRead;

        public ModelEngine(ModelEngineOptions? options = null)
        {
            this._options = options ?? new ModelEngineOptions();
            ArgumentOutOfRangeException.ThrowIfLessThan(this._options.MaxBatch, 2, nameof(options));
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(this._options.TombstoneRetention, TimeSpan.Zero, nameof(options));
            ArgumentOutOfRangeException.ThrowIfLessThan(this._options.TombstoneCleanupInterval, TimeSpan.Zero, nameof(options));
        }

        /// <summary>Gets the state of the connection of the engine.</summary>
        public ModelEngineState State => this._state;

        /// <summary>Gets the number of the scene collection that the model is of: it goes up each time that OBS Studio has changed its scene collection. It is for tests and for the host.</summary>
        internal int Epoch => this._epoch;

        /// <summary>Gets whether OBS Studio is changing its scene collection: between the event that says so and the one that says that it has done so. It is for tests.</summary>
        internal bool IsSwitching => this._switching;

        /// <summary>Gets the settings that the engine was made with. It is for tests.</summary>
        internal ModelEngineOptions Options => this._options;

        /// <summary>Gets the number of things that the engine remembers because of what it was told was removed (see <see cref="ModelEngineOptions.TombstoneRetention"/>). It is for tests.</summary>
        public int RememberedCount => this._gone.Count + this._goneItems.Count + this._removedUnknown.Count + this._readReleased.Count + this._readReleased.SettlingCount;

        /// <summary>Gets the number of scene items that a read took away and that the events have not told about, which keep the engine from concluding from a count. It is for tests.</summary>
        public int HeldItemCount => this._readReleased.Count;

        /// <summary>Gets the number of things that the engine let go of because they were old. It is for tests.</summary>
        public long LetGoCount => this._letGo;

        /// <summary>
        /// Gets whether nothing is waiting for a read, nothing is being read, and everything that the model holds is complete: the model
        /// is as correct as it can be made until something new arrives.
        /// </summary>
        public bool IsSettled => this._state != ModelEngineState.Disconnected && this.PendingWork() == 0;

        /// <summary>
        /// Gets the time at which the engine wants to be called again although nothing arrives (a read that waits for its turn), or null. While there
        /// is no connection there is nothing to read, so it is null then; and so it is while OBS Studio changes its scene collection, when nothing is read.
        /// </summary>
        public TimeSpan? NextWake
        {
            get
            {
                if (this._state == ModelEngineState.Disconnected || this._switching)
                {
                    return null;
                }

                TimeSpan? earliest = null;
                foreach (var scope in this.AllScopes())
                {
                    if (scope.Dirty && !scope.InFlight && (earliest is null || scope.NotBefore < earliest))
                    {
                        earliest = scope.NotBefore;
                    }
                }

                // An output that is starting or stopping is looked at when the step has lasted as long as it may (the scope is marked then, and counts as work from then on).
                foreach (var output in this._outputs)
                {
                    if (output.StepCheckAt is { } due && !output.Scope.Dirty && !output.Scope.InFlight && (earliest is null || due < earliest))
                    {
                        earliest = due;
                    }
                }

                return earliest;
            }
        }

        /// <summary>Tells the engine that the handshake of a connection is complete: the model is read again, and merged with what it holds.</summary>
        public void OnSessionStarted(int sessionId, long seq, TimeSpan now)
        {
            this._now = now;
            this._sessionId = sessionId;
            this._state = ModelEngineState.Synchronizing;

            // A switch of the scene collection that the connection before had begun, and that was not seen to its end: OBS Studio left the collection that the model holds, and
            // what it has loaded now (maybe that very collection, with the UUIDs that it had) is read as the first collection of a connection is, with nothing of the old one
            // merged into it. A connection that ended without a switch keeps the model, and merges what it reads by UUID.
            if (this._switching)
            {
                this.ForgetCollection();
            }

            this._scenesRead = this._inputsRead = this._programRead = this._studioRead = false;

            // OBS Studio that says that it is exiting does not come back as the same one: a connection that is made after it is made to one that was started again.
            this._exiting.ApplyDerived(false, seq);
            foreach (var output in this._outputs)
            {
                output.Read = false;
                output.StepCheckAt = null;
            }

            // What another connection saw does not hold for this one: a source that was removed there can be here (the scene collection that has
            // it was loaded again), and what its scene items were is read again before a removal is taken to be a destruction.
            this._gone.Clear();
            this._goneItems.Clear();
            this._removedUnknown.Clear();
            this._readReleased.Clear();
            this._lastCleanup = now;
            // What the model believes about the lifecycle of a source comes from the events that this connection saw, and the reads that follow: on
            // a new connection it starts as it does on a first one. A scene or an input that was Detached can be live now (OBS was started again
            // from a collection that has it): a scene that the list has is live, and an input that is in the list is, until an event removes it.
            this._unresolved.Clear();
            foreach (var scene in this._scenes.Values)
            {
                scene.ItemsKnown = false;
                scene.RemovedAnnounced = false;
                scene.RemovalUnresolved = false;
                scene.CheckAfter = 0;
                if (scene.State == SourceState.Detached && !scene.Provisional)
                {
                    scene.State = SourceState.Live;
                }
            }

            foreach (var input in this._inputs.Values)
            {
                input.RemovedAnnounced = false;
                input.RemovalUnresolved = false;
                input.CheckAfter = 0;
                if (input.State == SourceState.Detached)
                {
                    input.State = SourceState.Live;
                }
            }

            foreach (var scope in this.AllScopes())
            {
                scope.InFlight = false;
                scope.Attempts = 0;
                scope.NotBefore = TimeSpan.Zero;
                scope.Dirty = true;
            }
        }

        /// <summary>Tells the engine that the connection ended: reads that were under way will never be answered.</summary>
        public void OnSessionEnded(int sessionId, long seq, TimeSpan now)
        {
            this._now = now;
            this._state = ModelEngineState.Disconnected;
            foreach (var scope in this.AllScopes())
            {
                scope.InFlight = false;
            }

            this.Emit(ModelChangeKind.Disconnected, Guid.Empty);
        }

        // Lets go of what the engine remembers about removals when it is not needed any more (see TombstoneRetention), and asks for a list when a source that
        // was removed long ago is not confirmed by one. It looks at most every TombstoneCleanupInterval, however many messages arrive.
        private void CleanUp()
        {
            if (this._now - this._lastCleanup < this._options.TombstoneCleanupInterval)
            {
                return;
            }

            this._lastCleanup = this._now;
            var before = this._now - this._options.TombstoneRetention;
            var freed = new List<Guid>();
            this._letGo += this._gone.Prune(before) + this._removedUnknown.Prune(before) + this._goneItems.Prune(before) + this._readReleased.Prune(before, freed);

            // What was held for the events that could still come (see HeldByRead) is let go of, and the count of the scene items that use a source can be trusted
            // again: a removed source that nothing uses is destroyed now, as it is when the last scene item goes.
            foreach (var source in freed)
            {
                this.JudgeAgain(source);
            }

            // A source stays remembered until a list shows that OBS is done with it. The lists are read for other reasons too, but not always: they are read for this.
            // A read of the list that is under way was sent after these removals (they are older than the time that a read can take), so it confirms them as
            // well as another read would: a part that is read already is not marked again.
            if (!this._inputsScope.InFlight && (this._gone.NeedsConfirmation(SourceKind.Input, before) || this._removedUnknown.NeedsConfirmation(SourceKind.Input, before)))
            {
                this._inputsScope.MarkDirty();
            }

            if (!this._scenesScope.InFlight && (this._gone.NeedsConfirmation(SourceKind.Scene, before) || this._removedUnknown.NeedsConfirmation(SourceKind.Scene, before)))
            {
                this._scenesScope.MarkDirty();
            }

            // Judging a source again can destroy what was the last that the model waited for (an input that was removed, whose audio state cannot be read): the state
            // that the callers set before this look at what is remembered does not know. Every caller gets the state that goes with the model that it is left with.
            this.UpdateState();
        }

        /// <summary>
        /// Asks for the next read: the parts of the model that need one and whose turn it is, as one serial batch. Returns false when there is
        /// nothing to read.
        /// </summary>
        /// <param name="lastIngress">The number of the last message that has arrived, at this moment.</param>
        /// <param name="now">The time.</param>
        /// <param name="ticket">The requests to send.</param>
        public bool TryTakeRead(long lastIngress, TimeSpan now, out ReadTicket ticket)
        {
            this._now = now;
            ticket = null!;
            if (this._state == ModelEngineState.Disconnected || this._switching)
            {
                // Nothing is asked of OBS Studio while it changes its scene collection: it answers 207, and a request that it is in the middle of can do harm.
                return false;
            }

            this.CleanUp();

            var requests = new List<ReadRequest>();
            var max = this._options.MaxBatch;

            // Takes the requests of a part when they fit in what is left of the batch; a part that does not fit waits for the next one. A part that is bigger than a whole batch is
            // read alone.
            bool Take(ScopeState scope, params ReadRequest[] taken)
            {
                if (requests.Count + taken.Length > max && !(requests.Count == 0 && taken.Length > max))
                {
                    return false;
                }

                scope.Dirty = false;
                scope.InFlight = true;
                scope.SentSeq = lastIngress;
                requests.AddRange(taken);
                return true;
            }

            if (this._scenesScope.IsReady(now))
            {
                Take(this._scenesScope, new ReadRequest(ReadKind.Scenes, RequestType.GetSceneList, null, Guid.Empty));
            }

            if (this._inputsScope.IsReady(now))
            {
                Take(this._inputsScope, new ReadRequest(ReadKind.Inputs, RequestType.GetInputList, null, Guid.Empty));
            }

            if (this._programScope.IsReady(now))
            {
                Take(this._programScope, new ReadRequest(ReadKind.Program, RequestType.GetCurrentProgramScene, null, Guid.Empty));
            }

            if (this._studioScope.IsReady(now))
            {
                Take(this._studioScope, new ReadRequest(ReadKind.StudioMode, RequestType.GetStudioModeEnabled, null, Guid.Empty));
            }

            foreach (var output in this._outputs)
            {
                // A start or a stop that has lasted as long as it may is looked at (see ModelEngineOptions.OutputStepTimeout).
                if (output.StepCheckAt is { } due && due <= now && !output.Scope.Dirty && !output.Scope.InFlight)
                {
                    output.Scope.MarkDirty();
                }

                if (output.Scope.IsReady(now))
                {
                    Take(output.Scope, new ReadRequest(output.ReadKind, output.StatusRequest, null, Guid.Empty));
                }
            }

            foreach (var scene in this._scenes.Values.OrderBy(s => s.Index.Value).ThenBy(s => s.Uuid))
            {
                if (requests.Count >= max)
                {
                    break;
                }

                if (scene.ItemsScope.IsReady(now))
                {
                    var type = scene.IsGroup ? RequestType.GetGroupSceneItemList : RequestType.GetSceneItemList;
                    Take(scene.ItemsScope, new ReadRequest(ReadKind.Items, type, new { sceneUuid = scene.Uuid }, scene.Uuid));
                }
            }

            foreach (var input in this._inputs.Values.OrderBy(i => i.Uuid))
            {
                if (requests.Count >= max)
                {
                    break;
                }

                if (input.HasAudio && input.AudioScope.IsReady(now))
                {
                    Take(input.AudioScope, AudioRequests(input.Uuid));
                }

                if (input.SettingsScope.IsReady(now))
                {
                    Take(input.SettingsScope, new ReadRequest(ReadKind.InputSettings, RequestType.GetInputSettings, new { inputUuid = input.Uuid }, input.Uuid));
                }

                if (input.IsMedia && input.MediaScope.IsReady(now))
                {
                    Take(input.MediaScope, new ReadRequest(ReadKind.MediaStatus, RequestType.GetMediaInputStatus, new { inputUuid = input.Uuid }, input.Uuid));
                }
            }

            // The filters of every scene and input, scenes first.
            foreach (var scene in this._scenes.Values.OrderBy(s => s.Index.Value).ThenBy(s => s.Uuid))
            {
                if (requests.Count >= max)
                {
                    break;
                }

                if (scene.FiltersScope.IsReady(now))
                {
                    Take(scene.FiltersScope, new ReadRequest(ReadKind.Filters, RequestType.GetSourceFilterList, new { sourceUuid = scene.Uuid }, scene.Uuid));
                }
            }

            foreach (var input in this._inputs.Values.OrderBy(i => i.Uuid))
            {
                if (requests.Count >= max)
                {
                    break;
                }

                if (input.FiltersScope.IsReady(now))
                {
                    Take(input.FiltersScope, new ReadRequest(ReadKind.Filters, RequestType.GetSourceFilterList, new { sourceUuid = input.Uuid }, input.Uuid));
                }
            }

            if (requests.Count == 0)
            {
                return false;
            }

            ticket = new ReadTicket(++this._nextTicket, this._sessionId, this._epoch, lastIngress, now, requests);
            return true;
        }

        /// <summary>Returns the changes of the model since the last call, in the order in which they happened.</summary>
        public IReadOnlyList<ModelChange> TakeChanges()
        {
            if (this._changes.Count == 0)
            {
                return [];
            }

            var changes = this._changes.ToArray();
            this._changes.Clear();
            return changes;
        }

        // The number of things that are waiting: parts that need a read or are being read, and objects that are not complete.
        private int PendingWork()
        {
            var pending = 0;
            foreach (var scope in this.AllScopes())
            {
                if (scope.Dirty || scope.InFlight)
                {
                    pending++;
                }
            }

            foreach (var scene in this._scenes.Values)
            {
                if (!scene.ItemsKnown || scene.Items.Values.Any(item => !item.Known))
                {
                    pending++;
                }
            }

            foreach (var input in this._inputs.Values)
            {
                if (!input.IsComplete)
                {
                    pending++;
                }
            }

            return pending + this._unresolved.Count + (this._switching ? 1 : 0) + (this._scenesRead && this._inputsRead && this._programRead && this._studioRead && this.OutputsRead() ? 0 : 1);
        }

        // Whether every output was read on this connection (or was found to be one that OBS Studio does not have).
        private bool OutputsRead()
        {
            foreach (var output in this._outputs)
            {
                if (!output.Read)
                {
                    return false;
                }
            }

            return true;
        }

        private IEnumerable<ScopeState> AllScopes()
        {
            yield return this._scenesScope;
            yield return this._inputsScope;
            yield return this._programScope;
            yield return this._studioScope;
            foreach (var output in this._outputs)
            {
                yield return output.Scope;
            }

            foreach (var scene in this._scenes.Values)
            {
                yield return scene.ItemsScope;
            }

            // The audio state of an input without audio is never read, so it has nothing to wait for; nor has the media status of an input that is not a media input. The settings of every input are read.
            foreach (var input in this._inputs.Values)
            {
                if (input.HasAudio)
                {
                    yield return input.AudioScope;
                }

                yield return input.SettingsScope;
                if (input.IsMedia)
                {
                    yield return input.MediaScope;
                }
            }

            // Every scene and input has filters to read.
            foreach (var scene in this._scenes.Values)
            {
                yield return scene.FiltersScope;
            }

            foreach (var input in this._inputs.Values)
            {
                yield return input.FiltersScope;
            }
        }

        // Moves the state to Synchronized when everything is settled, and tells so.
        private void UpdateState()
        {
            if (this._state == ModelEngineState.Synchronizing && this.PendingWork() == 0)
            {
                this._state = ModelEngineState.Synchronized;
                this.Emit(ModelChangeKind.Synchronized, Guid.Empty);
            }
        }

        private void Emit(ModelChangeKind kind, Guid subject, int itemId = 0, string? text = null, object? oldValue = null, object? newValue = null)
        {
            this._changes.Add(new ModelChange(kind, subject, itemId, text, oldValue, newValue));
        }

        // The wait before the next repeat of a read that has to be repeated: 250 ms, then twice as long each time, up to the maximum.
        private TimeSpan Backoff(ScopeState scope)
        {
            var wait = TimeSpan.FromTicks(this._options.MinBackoff.Ticks << Math.Min(scope.Attempts, 20));
            scope.Attempts++;
            return wait > this._options.MaxBackoff ? this._options.MaxBackoff : wait;
        }
    }
}
