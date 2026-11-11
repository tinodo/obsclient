namespace OBSStudioClient.Model
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Events;

    /// <summary>
    /// Keeps the levels of the audio that OBS Studio sends (the event <c>InputVolumeMeters</c>) in the inputs of the model, and tells the application of them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The levels are a stream and not state: nothing is read, nothing is sent back, and nothing waits for an older message, so they do not go through the engine. The receive loop of the client calls
    /// <see cref="Observe"/> for every message and the levels are in <see cref="ObsInput.Levels"/> when it returns (nothing here blocks: a lock that only this class takes, for as long as it takes to map
    /// twenty inputs).
    /// </para>
    /// <para>
    /// OBS Studio lists the inputs that are on the air and have audio, and never says that an input stopped: an input that was listed and is not any more has no levels (<see cref="ObsInput.Levels"/> is null), and the end of
    /// the connection takes all of them.
    /// </para>
    /// <para>
    /// The application is told through the <see cref="ModelNotifier"/>, but not one notification for each message: a handler that is slower than OBS Studio would fill the queue, and hold up the notifications behind it. At
    /// most one is queued at a time, and it tells what changed since the last one that was raised, with the newest levels (an input that ended and is listed again before it ran is not an input that ended).
    /// </para>
    /// </remarks>
    internal sealed class ModelAudioLevels
    {
        private readonly Func<IReadOnlyList<ObsInput>> _inputsOfTheModel;
        private readonly Action<ObsAudioLevelsEventArgs> _raise;
        private readonly ModelNotifier _notifier;
        private readonly object _gate = new();
        private readonly Action _deliver;
        private readonly Dictionary<Guid, ObsInput> _byUuid = [];
        private readonly HashSet<ObsInput> _changed = [];
        private readonly HashSet<ObsInput> _ended = [];
        private IReadOnlyList<ObsInput>? _snapshot;
        private ObsInput[] _listed = [];
        private ObsAudioLevels[] _latest = [];
        private int _session;
        private int _queued;

        /// <summary>Initializes a new instance of the <see cref="ModelAudioLevels"/> class.</summary>
        /// <param name="inputs">What gives the inputs that OBS Studio still has, as the model shows them now (the ones that were removed and are kept alive for a scene item as well: their levels go on).</param>
        /// <param name="notifier">The notifier that tells the application.</param>
        /// <param name="raise">What raises the event of the model.</param>
        public ModelAudioLevels(Func<IReadOnlyList<ObsInput>> inputs, ModelNotifier notifier, Action<ObsAudioLevelsEventArgs> raise)
        {
            this._inputsOfTheModel = inputs;
            this._notifier = notifier;
            this._raise = raise;
            this._deliver = this.Deliver;
            notifier.Dropped = this.OnDropped;
        }

        /// <summary>
        /// Looks at what the receive loop of the client read: the start and the end of a connection, which levels depend on, and the levels themselves.
        /// </summary>
        /// <param name="item">What was read.</param>
        /// <returns>Whether the item was the levels, and is dealt with (the rest of the model has no use for it).</returns>
        public bool Observe(in IngressItem item)
        {
            switch (item.Kind)
            {
                case IngressKind.SessionStarted:
                    lock (this._gate)
                    {
                        this._session = item.SessionId;
                    }

                    return false;
                case IngressKind.SessionEnded:
                    this.EndSession(item.SessionId);
                    return false;
                case IngressKind.Event when item.Event!.EventType == EventType.InputVolumeMeters:
                    if (item.Event.EventData is InputVolumeMetersEventArgs meters)
                    {
                        this.Process(item.SessionId, meters.Inputs);
                    }

                    return true;
                default:
                    return false;
            }
        }

        private static ObsChannelLevel[] ChannelsOf(float[][]? levels)
        {
            if (levels is null || levels.Length == 0)
            {
                return [];
            }

            var channels = new ObsChannelLevel[levels.Length];
            for (var index = 0; index < channels.Length; index++)
            {
                var values = levels[index];
                channels[index] = new ObsChannelLevel(At(values, 0), At(values, 1), At(values, 2));
            }

            return channels;
        }

        // OBS Studio sends three numbers for a channel; a message with fewer is read as far as it goes.
        private static float At(float[]? values, int index)
        {
            return values is not null && index < values.Length ? values[index] : 0f;
        }

        private void Process(int session, InputVolumeMeterResponse[] entries)
        {
            bool wake;
            lock (this._gate)
            {
                // A message of a connection that is not the one that the model is connected to (it ended, or has not started) says nothing about it.
                if (session != this._session)
                {
                    return;
                }

                var inputs = this.InputsByUuid();
                var levels = new List<ObsAudioLevels>(entries.Length);
                foreach (var entry in entries)
                {
                    // An input that the model does not have (yet: it was just made) is skipped: the next message has it. An input that is listed twice counts once.
                    if (entry.InputUuid is not { } uuid || !inputs.TryGetValue(uuid, out var input) || levels.Exists(known => known.Input == input))
                    {
                        continue;
                    }

                    var fresh = new ObsAudioLevels(input, ChannelsOf(entry.InputLevelsMul));
                    if (input.SetLevels(fresh))
                    {
                        this._changed.Add(input);
                        this._ended.Remove(input);
                        levels.Add(fresh);
                    }
                    else
                    {
                        levels.Add(input.Levels!);
                    }
                }

                foreach (var previous in this._listed)
                {
                    if (!levels.Exists(known => known.Input == previous) && previous.SetLevels(null))
                    {
                        this._changed.Add(previous);
                        this._ended.Add(previous);
                    }
                }

                this._listed = levels.ConvertAll(known => known.Input).ToArray();
                this._latest = levels.ToArray();
                wake = this._changed.Count > 0;
            }

            if (wake)
            {
                this.Wake();
            }
        }

        private void EndSession(int session)
        {
            lock (this._gate)
            {
                if (session != this._session)
                {
                    return;
                }

                this._session = 0;
                foreach (var input in this._listed)
                {
                    if (input.SetLevels(null))
                    {
                        this._changed.Add(input);
                        this._ended.Add(input);
                    }
                }

                this._listed = [];
                this._latest = [];
            }

            this.Wake();
        }

        // The inputs of the model by UUID: made again only when the model published another set of inputs, so a message costs no more than a lookup for each input in it.
        private Dictionary<Guid, ObsInput> InputsByUuid()
        {
            var snapshot = this._inputsOfTheModel();
            if (!ReferenceEquals(snapshot, this._snapshot))
            {
                this._byUuid.Clear();
                foreach (var input in snapshot)
                {
                    this._byUuid[input.Uuid] = input;
                }

                this._snapshot = snapshot;
            }

            return this._byUuid;
        }

        // Queues the notification, unless one is queued already: it tells everything that is there when it runs.
        private void Wake()
        {
            if (Interlocked.CompareExchange(ref this._queued, 1, 0) == 0 && !this._notifier.TryEnqueue(this._deliver))
            {
                Volatile.Write(ref this._queued, 0);
            }
        }

        // A notification that was queued was dropped: the one that is waited for may be it. Only that one is waited for: when another notification was dropped, the one of the
        // levels may still be waiting in the context, and queuing another would deliver twice.
        private void OnDropped(Action dropped)
        {
            if (ReferenceEquals(dropped, this._deliver))
            {
                Volatile.Write(ref this._queued, 0);
            }
        }

        // Runs on the lane of the notifications. The flag is reset in the same step that takes what changed, so what changes afterwards queues a notification of its own.
        private void Deliver()
        {
            ObsAudioLevels[] levels;
            ObsInput[] ended;
            ObsInput[] changed;
            lock (this._gate)
            {
                Volatile.Write(ref this._queued, 0);
                if (this._changed.Count == 0)
                {
                    return;
                }

                levels = this._latest;
                ended = [.. this._ended];
                changed = [.. this._changed];
                this._ended.Clear();
                this._changed.Clear();
            }

            this._raise(new ObsAudioLevelsEventArgs(levels, ended));
            foreach (var input in changed)
            {
                input.RaisePropertyChanged(nameof(ObsInput.Levels));
            }
        }
    }
}
