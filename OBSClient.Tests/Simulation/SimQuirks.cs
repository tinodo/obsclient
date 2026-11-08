namespace OBSStudioClient.Tests.Simulation
{
    /// <summary>
    /// How the events of a change and the response to the request that caused it are ordered when they leave OBS Studio. obs-websocket
    /// sends the response from the thread that ran the request and the events from a thread pool, so either can be first (fact 1).
    /// </summary>
    internal enum SimEventResponseOrder
    {
        /// <summary>Every event of the change is sent before the response.</summary>
        EventFirst,

        /// <summary>The response is sent before every event of the change.</summary>
        ResponseFirst,

        /// <summary>
        /// Each change decides for itself: every event is, with a chance of one half, sent before the response, unless an event before it
        /// came after. The response can therefore be in the middle of the events of a change (fact 26).
        /// </summary>
        Random,
    }

    /// <summary>A range of whole milliseconds. A range that is none is always zero.</summary>
    /// <param name="MinMs">The shortest time, in milliseconds.</param>
    /// <param name="MaxMs">The longest time, in milliseconds.</param>
    internal readonly record struct SimRange(int MinMs, int MaxMs)
    {
        /// <summary>Gets the range that is always zero.</summary>
        public static SimRange None => default;

        /// <summary>Gets a value indicating whether the range is always zero.</summary>
        public bool IsNone => MinMs <= 0 && MaxMs <= 0;

        /// <summary>Gets a range of exactly one duration.</summary>
        public static SimRange Exactly(int milliseconds) => new(milliseconds, milliseconds);

        /// <summary>Draws a duration from the range. A range of one duration does not use the generator.</summary>
        public TimeSpan Sample(SimRandom random)
        {
            if (MaxMs <= MinMs)
            {
                return TimeSpan.FromMilliseconds(Math.Max(MinMs, 0));
            }

            return TimeSpan.FromMilliseconds(random.Next(MinMs, MaxMs + 1));
        }
    }

    /// <summary>
    /// What a simulated OBS Studio does that a perfect one would not. Each quirk is a measurement of a real OBS Studio (see the facts of the
    /// design of the model) and each can be switched on its own: take a preset (<see cref="Ideal"/>, <see cref="Real"/>,
    /// <see cref="Hostile"/>) and change what a test is about, for example <c>SimQuirks.Ideal with { DuplicateSceneCreated = true }</c>.
    /// </summary>
    internal sealed record SimQuirks
    {
        /// <summary>
        /// Gets what a perfect OBS Studio would be: everything happens at once and in order, there is no lag and nothing is sent twice. The
        /// events of a change follow its response, in the order in which they were emitted. The program scene is cut (no transition time).
        /// </summary>
        public static SimQuirks Ideal { get; } = new()
        {
            EventResponseOrder = SimEventResponseOrder.ResponseFirst,
            TransitionDuration = TimeSpan.Zero,
        };

        /// <summary>
        /// Gets what was measured in OBS Studio 33.0.0 with obs-websocket 5.7.6 on Windows. This is the default of <see cref="SimObs"/>.
        /// </summary>
        public static SimQuirks Real { get; } = new()
        {
            EventResponseOrder = SimEventResponseOrder.Random,
            ResponseLatency = new SimRange(0, 3),
            EventLatency = new SimRange(0, 15),
            EventLatencyTailChance = 0.04,
            EventLatencyTail = new SimRange(16, 50),
            InputSettingsChangedLatency = new SimRange(14, 50),
            InputSettingsChangedOutlierChance = 0.02,
            InputSettingsChangedOutlier = SimRange.Exactly(330),
            ReorderConcurrentRequests = true,
            ExecutionJitter = new SimRange(0, 8),
            DuplicateSceneCreated = true,
            SceneListLag = new SimRange(0, 2000),
            StaleSceneListChangedPayload = false,
            TransitionDuration = TimeSpan.FromMilliseconds(300),
            TransitionOverhead = new SimRange(0, 40),
            HangOnRemovedSceneRename = true,
            ParallelBatchMisorders = true,
            DestroyCascadeDelay = TimeSpan.FromMilliseconds(2),
            CollectionSwitchTime = new SimRange(100, 600),
        };

        /// <summary>
        /// Gets <see cref="Real"/> with wider latencies, a list of scenes that always lags, a <c>SceneListChanged</c> that can be stale, and events
        /// that are regularly delayed behind several later responses.
        /// </summary>
        public static SimQuirks Hostile { get; } = Real with
        {
            ResponseLatency = new SimRange(0, 10),
            EventLatency = new SimRange(0, 60),
            EventLatencyTailChance = 0.30,
            EventLatencyTail = new SimRange(60, 400),
            InputSettingsChangedLatency = new SimRange(14, 120),
            InputSettingsChangedOutlierChance = 0.10,
            InputSettingsChangedOutlier = new SimRange(330, 800),
            ExecutionJitter = new SimRange(0, 25),
            SceneListLag = new SimRange(200, 3000),
            StaleSceneListChangedPayload = true,
            TransitionOverhead = new SimRange(0, 80),
            CollectionSwitchTime = new SimRange(300, 4000),
            CreationEventsDuringSwitch = true,
        };

        /// <summary>Gets the order of the response and the events of a change.</summary>
        public SimEventResponseOrder EventResponseOrder { get; init; }

        /// <summary>Gets how long after a request has run its response is sent.</summary>
        public SimRange ResponseLatency { get; init; }

        /// <summary>Gets how long after a change an event is sent, for the events that have no latency of their own.</summary>
        public SimRange EventLatency { get; init; }

        /// <summary>Gets the chance that an event takes <see cref="EventLatencyTail"/> instead of <see cref="EventLatency"/>.</summary>
        public double EventLatencyTailChance { get; init; }

        /// <summary>Gets the long latency that an event sometimes has (fact 1: within 50 ms).</summary>
        public SimRange EventLatencyTail { get; init; }

        /// <summary>
        /// Gets a value indicating whether an event can be sent before an event of an earlier change, so that the events of two changes
        /// interleave according to their latencies. It is off in every preset. Events are sent from a thread pool (fact 1), so it can happen, but
        /// it was never seen: after bursts of 100 requests the last event always equalled the state of OBS Studio (fact 3). When it happens, two
        /// events for one value arrive in the wrong order, no client can tell, and the older value stays until something else tells.
        /// </summary>
        public bool ReorderEventsAcrossChanges { get; init; }

        /// <summary>Gets the latency of <c>InputSettingsChanged</c> (fact 2: 14 to 50 ms after the response). That event is not emitted yet.</summary>
        public SimRange InputSettingsChangedLatency { get; init; }

        /// <summary>Gets the chance that <c>InputSettingsChanged</c> takes <see cref="InputSettingsChangedOutlier"/>.</summary>
        public double InputSettingsChangedOutlierChance { get; init; }

        /// <summary>Gets the rare long latency of <c>InputSettingsChanged</c> (fact 2: once 330 ms).</summary>
        public SimRange InputSettingsChangedOutlier { get; init; }

        /// <summary>
        /// Gets a value indicating whether requests that are in flight together run in another order than the one they were sent in (fact 3:
        /// 100 requests that were not awaited ran out of order). A request that is received at time t then runs at t plus its jitter.
        /// </summary>
        public bool ReorderConcurrentRequests { get; init; }

        /// <summary>Gets the time between the arrival of a request and the moment it runs, when <see cref="ReorderConcurrentRequests"/> is on.</summary>
        public SimRange ExecutionJitter { get; init; }

        /// <summary>
        /// Gets a value indicating whether every scene that is created is announced twice (fact 10, obs-websocket#1364): <c>SceneCreated</c>,
        /// <c>SceneListChanged</c>, <c>SceneCreated</c>.
        /// </summary>
        public bool DuplicateSceneCreated { get; init; }

        /// <summary>
        /// Gets the lag of the list of scenes, drawn for each scene (fact 4): <c>GetSceneList</c> does not list a scene until that long after it
        /// was created, and still lists it until that long after it was removed. None means no lag.
        /// </summary>
        public SimRange SceneListLag { get; init; }

        /// <summary>
        /// Gets a value indicating whether the list of scenes in a <c>SceneListChanged</c> event also holds the scenes that were removed less
        /// than their lag ago (fact 10: the payload can list removed scenes).
        /// </summary>
        public bool StaleSceneListChangedPayload { get; init; }

        /// <summary>Gets the duration of the transition to another program scene (fact 2). Zero is a Cut.</summary>
        public TimeSpan TransitionDuration { get; init; } = TimeSpan.Zero;

        /// <summary>
        /// Gets the time that a transition takes beyond its duration, before it ends (fact 25: a Fade of 300 ms ended at about 340 ms; fact 2: a
        /// Cut takes up to a few tens of milliseconds to be announced).
        /// </summary>
        public SimRange TransitionOverhead { get; init; }

        /// <summary>
        /// Gets a value indicating whether <c>SetSceneName</c> for a scene that was removed, but is still alive, is never answered (fact 9).
        /// </summary>
        public bool HangOnRemovedSceneRename { get; init; }

        /// <summary>
        /// Gets a value indicating whether the responses of a request batch that runs in parallel come back in a random order (fact 16,
        /// obs-websocket#1317). Going by the source and the issue, the requestType and requestId of each result keep the order of the
        /// requests, and the status and data are those of the request that finished in that place.
        /// </summary>
        public bool ParallelBatchMisorders { get; init; }

        /// <summary>
        /// Gets how long after the <c>SceneRemoved</c> of a scene that is destroyed at once the events of the inputs that it destroys follow
        /// (fact 7: about 2 ms).
        /// </summary>
        public TimeSpan DestroyCascadeDelay { get; init; }

        /// <summary>
        /// Gets how long OBS Studio takes to change its scene collection: from <c>CurrentSceneCollectionChanging</c> to
        /// <c>CurrentSceneCollectionChanged</c>, drawn for each switch. During that time obs-websocket is not ready (it answers every request with
        /// 207 and sends no event). None means that the switch is done in the same instant, but still after the change that began it.
        /// </summary>
        /// <remarks>
        /// ASSUMPTION: the range is a guess. A switch has to load every source of the collection, which takes from a tenth of a second for a
        /// few simple sources to several seconds for capture devices. A measurement on a real OBS Studio (the time between the two events for a
        /// small and for a large collection) would replace it.
        /// </remarks>
        public SimRange CollectionSwitchTime { get; init; }

        /// <summary>
        /// Gets a value indicating whether the events of the sources that are created while a collection loads (<c>SceneCreated</c>,
        /// <c>InputCreated</c>, <c>SceneItemCreated</c>) are sent between <c>CurrentSceneCollectionChanging</c> and
        /// <c>CurrentSceneCollectionChanged</c>. obs-websocket 5.4.0 and later send no event while it is not ready, so they are not (it is off
        /// in <see cref="Real"/>). It is on in <see cref="Hostile"/>, for a client that has to cope with a server that does send them: the
        /// design notes of the model (fact 14) say that creation events can arrive in a switch, and what an older or another version
        /// does was not checked.
        /// </summary>
        public bool CreationEventsDuringSwitch { get; init; }
    }
}
