namespace OBSStudioClient.Studio.Engine
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

        /// <summary>GetInputMute.</summary>
        AudioMute,

        /// <summary>GetInputVolume (always right after GetInputMute of the same input).</summary>
        AudioVolume,
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
    /// The requests that the engine wants to have sent, as one serial batch, and what it needs to know about the moment they were sent.
    /// </summary>
    internal sealed class ReadTicket
    {
        public ReadTicket(int id, int sessionId, long sentSeq, TimeSpan sentAt, IReadOnlyList<ReadRequest> requests)
        {
            this.Id = id;
            this.SessionId = sessionId;
            this.SentSeq = sentSeq;
            this.SentAt = sentAt;
            this.Requests = requests;
        }

        /// <summary>Gets the number of the ticket.</summary>
        public int Id { get; }

        /// <summary>Gets the connection that the read belongs to: its result means nothing to another one.</summary>
        public int SessionId { get; }

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
    internal enum StudioEngineState
    {
        /// <summary>There is no connection (or it ended): what the model holds is the last that was known.</summary>
        Disconnected,

        /// <summary>The model is being read for the first time on this connection.</summary>
        Synchronizing,

        /// <summary>Everything was read, and nothing is waiting for a read.</summary>
        Synchronized,
    }

    /// <summary>
    /// The settings of the engine.
    /// </summary>
    internal sealed class StudioEngineOptions
    {
        /// <summary>
        /// Gets or sets how long a scene that an event announced may be missing from the list of scenes before the model concludes that it
        /// does not exist (the list of OBS lags behind its events: about 2 seconds were measured).
        /// </summary>
        public TimeSpan CreationGrace { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>Gets or sets the number of requests in one read. It cannot be less than 2: an audio state is read with two requests.</summary>
        public int MaxBatch { get; set; } = 50;

        /// <summary>Gets or sets how long the engine waits before it checks the list of inputs after an input lost its last scene item.</summary>
        public TimeSpan InputCheckDelay { get; set; } = TimeSpan.FromMilliseconds(200);

        /// <summary>Gets or sets the wait before the first repeat of a read.</summary>
        public TimeSpan MinBackoff { get; set; } = TimeSpan.FromMilliseconds(250);

        /// <summary>Gets or sets the longest wait before a repeat of a read.</summary>
        public TimeSpan MaxBackoff { get; set; } = TimeSpan.FromSeconds(2);
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
    internal sealed partial class StudioEngine
    {
        private readonly StudioEngineOptions _options;
        private readonly Dictionary<Guid, SceneNode> _scenes = new();
        private readonly Dictionary<Guid, InputNode> _inputs = new();
        private readonly Dictionary<Guid, HashSet<ItemKey>> _refs = new();

        // What OBS destroyed, and what was removed before the model knew it. Both guard against the messages that are older than the removal: a
        // read that was sent before it, the second copy of a creation event. They are for one connection: another one reads everything again,
        // and a source that was removed can be there again (the scene collection that had it was loaded again).
        private readonly HashSet<Guid> _gone = new();
        private readonly HashSet<ItemKey> _goneItems = new();
        private readonly HashSet<Guid> _removedUnknown = new();

        // The sources that were removed while the model did not know their scene items yet: whether OBS destroyed them is not known.
        private readonly HashSet<Guid> _unresolved = new();
        private readonly List<StudioChange> _changes = new();
        private readonly ScopeState _scenesScope = new();
        private readonly ScopeState _inputsScope = new();
        private readonly ScopeState _programScope = new();
        private readonly ScopeState _studioScope = new();
        private Cell<Guid?> _programUuid = new(null, 0);
        private Cell<bool> _studioMode = new(false, 0);
        private StudioEngineState _state = StudioEngineState.Disconnected;
        private TimeSpan _now;
        private int _nextTicket;
        private int _sessionId;
        private bool _scenesRead;
        private bool _inputsRead;
        private bool _programRead;
        private bool _studioRead;

        public StudioEngine(StudioEngineOptions? options = null)
        {
            this._options = options ?? new StudioEngineOptions();
            ArgumentOutOfRangeException.ThrowIfLessThan(this._options.MaxBatch, 2, nameof(options));
        }

        /// <summary>Gets the state of the connection of the engine.</summary>
        public StudioEngineState State => this._state;

        /// <summary>
        /// Gets whether nothing is waiting for a read, nothing is being read, and everything that the model holds is complete: the model
        /// is as correct as it can be made until something new arrives.
        /// </summary>
        public bool IsSettled => this._state != StudioEngineState.Disconnected && this.PendingWork() == 0;

        /// <summary>
        /// Gets the time at which the engine wants to be called again although nothing arrives (a read that waits for its turn), or null. While there
        /// is no connection there is nothing to read, so it is null then.
        /// </summary>
        public TimeSpan? NextWake
        {
            get
            {
                if (this._state == StudioEngineState.Disconnected)
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

                return earliest;
            }
        }

        /// <summary>Tells the engine that the handshake of a connection is complete: the model is read again, and merged with what it holds.</summary>
        public void OnSessionStarted(int sessionId, long seq, TimeSpan now)
        {
            this._now = now;
            this._sessionId = sessionId;
            this._state = StudioEngineState.Synchronizing;
            this._scenesRead = this._inputsRead = this._programRead = this._studioRead = false;

            // What another connection saw does not hold for this one: a source that was removed there can be here (the scene collection that has
            // it was loaded again), and what its scene items were is read again before a removal is taken to be a destruction.
            this._gone.Clear();
            this._goneItems.Clear();
            this._removedUnknown.Clear();
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
            this._state = StudioEngineState.Disconnected;
            foreach (var scope in this.AllScopes())
            {
                scope.InFlight = false;
            }

            this.Emit(StudioChangeKind.Disconnected, Guid.Empty);
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
            if (this._state == StudioEngineState.Disconnected)
            {
                return false;
            }

            var requests = new List<ReadRequest>();
            var max = this._options.MaxBatch;

            // Takes the requests of a part when they fit in what is left of the batch; a part that does not fit waits for the next one.
            bool Take(ScopeState scope, params ReadRequest[] taken)
            {
                if (requests.Count + taken.Length > max)
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
                    Take(
                        input.AudioScope,
                        new ReadRequest(ReadKind.AudioMute, RequestType.GetInputMute, new { inputUuid = input.Uuid }, input.Uuid),
                        new ReadRequest(ReadKind.AudioVolume, RequestType.GetInputVolume, new { inputUuid = input.Uuid }, input.Uuid));
                }
            }

            if (requests.Count == 0)
            {
                return false;
            }

            ticket = new ReadTicket(++this._nextTicket, this._sessionId, lastIngress, now, requests);
            return true;
        }

        /// <summary>Returns the changes of the model since the last call, in the order in which they happened.</summary>
        public IReadOnlyList<StudioChange> TakeChanges()
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

            return pending + this._unresolved.Count + (this._scenesRead && this._inputsRead && this._programRead && this._studioRead ? 0 : 1);
        }

        private IEnumerable<ScopeState> AllScopes()
        {
            yield return this._scenesScope;
            yield return this._inputsScope;
            yield return this._programScope;
            yield return this._studioScope;
            foreach (var scene in this._scenes.Values)
            {
                yield return scene.ItemsScope;
            }

            // The audio state of an input without audio is never read, so it has nothing to wait for.
            foreach (var input in this._inputs.Values)
            {
                if (input.HasAudio)
                {
                    yield return input.AudioScope;
                }
            }
        }

        // Moves the state to Synchronized when everything is settled, and tells so.
        private void UpdateState()
        {
            if (this._state == StudioEngineState.Synchronizing && this.PendingWork() == 0)
            {
                this._state = StudioEngineState.Synchronized;
                this.Emit(StudioChangeKind.Synchronized, Guid.Empty);
            }
        }

        private void Emit(StudioChangeKind kind, Guid subject, int itemId = 0, string? text = null, object? oldValue = null, object? newValue = null)
        {
            this._changes.Add(new StudioChange(kind, subject, itemId, text, oldValue, newValue));
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
