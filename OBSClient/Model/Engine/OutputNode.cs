namespace OBSStudioClient.Model.Engine
{
    using OBSStudioClient.Enums;

    /// <summary>
    /// What the model holds about one output of OBS Studio: the stream, the recording, the virtual camera or the replay buffer.
    /// </summary>
    /// <remarks>
    /// An output does not belong to a scene collection, so a change of the scene collection leaves it as it is; a new connection reads it again.
    /// </remarks>
    internal sealed class OutputNode
    {
        /// <summary>The status, and the numbers that tell how new it is. A field and not a property: the cell is changed in place.</summary>
        public Cell<ObsOutputStatus> Status = new(ObsOutputStatus.Stopped, 0);

        public OutputNode(ObsOutputKind kind)
        {
            this.Kind = kind;
        }

        /// <summary>Gets which output it is.</summary>
        public ObsOutputKind Kind { get; }

        /// <summary>Gets the part of the model that is the status: it is read with one request.</summary>
        public ScopeState Scope { get; } = new();

        /// <summary>Gets or sets whether a read (or the answer that OBS Studio has no such output to read) was applied on this connection.</summary>
        public bool Read { get; set; }

        /// <summary>Gets or sets the number of events that said that the output started or is starting.</summary>
        public int StartsSeen { get; set; }

        /// <summary>Gets or sets the number of events that said that the output stopped or is stopping.</summary>
        public int StopsSeen { get; set; }

        /// <summary>
        /// The file that OBS Studio last announced for the output, and the numbers that tell how new it is: the file of the recording (it says it when it starts, when it goes on in a new file, and when it
        /// stops), or the file of the replay that was saved last. Only events set it: no request says it. A field and not a property: the cell is changed in place. Null for the outputs that write no
        /// file, and until the first one was announced; it is not forgotten when a connection ends (a value that is not known to hold any more is still the last that was known, like the others).
        /// </summary>
        public Cell<string?> LastFile = new(null, 0);

        /// <summary>Gets or sets whether the recording that is under way (or ended last) went on in a new file since it started: what it says about its file when it stops is then the first one.</summary>
        public bool SplitSinceStart { get; set; }

        /// <summary>
        /// Gets or sets the connection in which an event last told what the output does, or 0 when none has. Within a connection the events are right: OBS Studio can show an output that stopped as
        /// running for a moment after it said so (seen with a recording, a real OBS Studio 33.0.0), so a read is applied to an output that is in no step only when no event of this connection has
        /// told yet (the state at the start of a connection, and what a connection before saw is not known to hold). It is also the connection that the step (starting, stopping) comes from:
        /// no event of a later connection will end it.
        /// </summary>
        public int EventSession { get; set; }

        /// <summary>
        /// Gets or sets the time at which an output that is starting or stopping is read, to see whether the step is over (see <see cref="ModelEngineOptions.OutputStepTimeout"/>); null when
        /// the output is in no step.
        /// </summary>
        public TimeSpan? StepCheckAt { get; set; }

        /// <summary>Gets what the read of the output is for.</summary>
        public ReadKind ReadKind => ReadKindOf(this.Kind);

        /// <summary>Gets the request that reads the status.</summary>
        public RequestType StatusRequest => this.Kind switch
        {
            ObsOutputKind.Stream => RequestType.GetStreamStatus,
            ObsOutputKind.Record => RequestType.GetRecordStatus,
            ObsOutputKind.VirtualCamera => RequestType.GetVirtualCamStatus,
            _ => RequestType.GetReplayBufferStatus,
        };

        /// <summary>Gets what the read of an output is for.</summary>
        /// <param name="kind">The output.</param>
        public static ReadKind ReadKindOf(ObsOutputKind kind)
        {
            return kind switch
            {
                ObsOutputKind.Stream => ReadKind.Stream,
                ObsOutputKind.Record => ReadKind.Record,
                ObsOutputKind.VirtualCamera => ReadKind.VirtualCamera,
                _ => ReadKind.ReplayBuffer,
            };
        }

        /// <summary>Gets which output a read is about.</summary>
        /// <param name="kind">What the read is for.</param>
        public static ObsOutputKind KindOf(ReadKind kind)
        {
            return kind switch
            {
                ReadKind.Stream => ObsOutputKind.Stream,
                ReadKind.Record => ObsOutputKind.Record,
                ReadKind.VirtualCamera => ObsOutputKind.VirtualCamera,
                _ => ObsOutputKind.ReplayBuffer,
            };
        }

        /// <summary>Gets whether a read is about an output.</summary>
        /// <param name="kind">What the read is for.</param>
        public static bool IsOutputRead(ReadKind kind)
        {
            return kind is ReadKind.Stream or ReadKind.Record or ReadKind.VirtualCamera or ReadKind.ReplayBuffer;
        }

        /// <summary>Gets whether an event is about an output.</summary>
        /// <param name="eventType">The type of the event.</param>
        public static bool IsOutputEvent(EventType eventType)
        {
            return eventType is EventType.StreamStateChanged or EventType.RecordStateChanged or EventType.VirtualcamStateChanged or EventType.ReplayBufferStateChanged
                or EventType.RecordFileChanged or EventType.ReplayBufferSaved;
        }

        /// <summary>
        /// Gets the status that a read says. A read can only say whether the output is running, and whether the stream is reconnecting or the recording is paused: it cannot tell an
        /// output that is starting or stopping from one that is not, so the step that the events of this connection gave is kept, for a while: OBS Studio does not always say that a start
        /// failed (a recording that cannot begin sends no event after the first), so a step that lasted too long is over when the output is not running. A step that an earlier connection saw
        /// is not kept at all: nothing ends it.
        /// </summary>
        /// <param name="kind">The output.</param>
        /// <param name="current">The status that the model holds.</param>
        /// <param name="stepIsCurrent">Whether the connection that the read was made on is the one that the events gave the step (starting, stopping) that the model holds.</param>
        /// <param name="stepExpired">Whether the step has lasted as long as the engine waits for it (see <see cref="ModelEngineOptions.OutputStepTimeout"/>): an output that is not running then is not on its way up.</param>
        /// <param name="active">Whether the output is running.</param>
        /// <param name="flagged">Whether the stream is reconnecting, or the recording is paused.</param>
        public static ObsOutputStatus FromRead(ObsOutputKind kind, ObsOutputStatus current, bool stepIsCurrent, bool stepExpired, bool active, bool flagged)
        {
            var step = stepIsCurrent && current is ObsOutputStatus.Starting or ObsOutputStatus.Stopping ? current : ObsOutputStatus.Stopped;
            if (!active)
            {
                return stepExpired ? ObsOutputStatus.Stopped : step;
            }

            if (flagged)
            {
                return kind == ObsOutputKind.Record ? ObsOutputStatus.Paused : ObsOutputStatus.Reconnecting;
            }

            return step == ObsOutputStatus.Stopping ? step : ObsOutputStatus.Started;
        }

        /// <summary>Gets the status that an event says.</summary>
        /// <param name="state">The state in the event.</param>
        /// <param name="status">The status.</param>
        /// <returns>False when the event says nothing (an unknown state).</returns>
        public static bool TryFromEvent(ObsOutputState state, out ObsOutputStatus status)
        {
            switch (state)
            {
                case ObsOutputState.OBS_WEBSOCKET_OUTPUT_STARTING:
                    status = ObsOutputStatus.Starting;
                    return true;
                case ObsOutputState.OBS_WEBSOCKET_OUTPUT_STARTED:
                case ObsOutputState.OBS_WEBSOCKET_OUTPUT_RECONNECTED:
                case ObsOutputState.OBS_WEBSOCKET_OUTPUT_RESUMED:
                    status = ObsOutputStatus.Started;
                    return true;
                case ObsOutputState.OBS_WEBSOCKET_OUTPUT_STOPPING:
                    status = ObsOutputStatus.Stopping;
                    return true;
                case ObsOutputState.OBS_WEBSOCKET_OUTPUT_STOPPED:
                    status = ObsOutputStatus.Stopped;
                    return true;
                case ObsOutputState.OBS_WEBSOCKET_OUTPUT_RECONNECTING:
                    status = ObsOutputStatus.Reconnecting;
                    return true;
                case ObsOutputState.OBS_WEBSOCKET_OUTPUT_PAUSED:
                    status = ObsOutputStatus.Paused;
                    return true;
                default:
                    status = ObsOutputStatus.Stopped;
                    return false;
            }
        }
    }
}
