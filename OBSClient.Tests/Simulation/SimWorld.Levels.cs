namespace OBSStudioClient.Tests.Simulation
{
    using System.Text.Json.Nodes;
    using static OBSStudioClient.Tests.Simulation.SimJson;

    /// <summary>
    /// The levels of the audio: <c>InputVolumeMeters</c>. Measured with OBS Studio 33.0.0 (LiveLevelsTests): obs-websocket sends one message every 50 ms to a client that subscribed to it, also when it has nothing to say, and
    /// lists in it the inputs that are on the air (<c>videoActive</c>) and have audio, in the order in which they came on the air, with the three numbers of each channel that are the magnitude and the peak with the volume of
    /// the input applied (0 while it is muted) and the peak as the input made it. An input that was just listed has no channels in its first message, and two in the next ones (a mono file is delivered as two channels).
    /// </summary>
    /// <remarks>
    /// The simulation has no clock that ticks: a message is sent when <see cref="MeterTick"/> is called, which is what makes a run of it deterministic, and what keeps <c>Settle</c> from running for ever.
    /// </remarks>
    internal sealed partial class SimWorld
    {
        private readonly List<SimInput> _meterOrder = [];
        private Dictionary<string, float[][]> _lastMeters = new(StringComparer.Ordinal);

        /// <summary>Gets what the last message of the volume meters said, by the UUID of the input: the three numbers of each channel (an empty list for an input that has no channels yet).</summary>
        public IReadOnlyDictionary<string, float[][]> LastMeters => _lastMeters;

        /// <summary>Gets the inputs that the next message lists, in the order in which it lists them.</summary>
        public IReadOnlyList<SimInput> MeteredInputs => _meterOrder.Where(input => !input.Destroyed).ToArray();

        /// <summary>One message of the volume meters: every input that is on the air and has audio, each with levels that depend on its volume and its mute.</summary>
        public void MeterTick()
        {
            _meterOrder.RemoveAll(input => input.Destroyed);
            var told = new Dictionary<string, float[][]>(StringComparer.Ordinal);
            var entries = new List<JsonNode?>();
            foreach (var input in _meterOrder)
            {
                var levels = input.MeterHeard ? NextLevels(input) : [];
                input.MeterHeard = true;
                told[input.UuidText] = levels;
                entries.Add(Obj(
                    ("inputLevelsMul", Arr(levels.Select(channel => (JsonNode?)Arr(channel.Select(number => (JsonNode?)JsonValue.Create(number)))))),
                    ("inputName", input.Name),
                    ("inputUuid", input.UuidText)));
            }

            _lastMeters = told;
            Emit("InputVolumeMeters", Obj(("inputs", Arr(entries))));
        }

        // obs-websocket makes a meter when an input comes on the air, and lets go of it when the input goes off the air: so the list is in the order of activation.
        private void TrackMeter(SimInput input)
        {
            var listed = input.Active && input.Kind.HasAudio;
            if (listed && !_meterOrder.Contains(input))
            {
                _meterOrder.Add(input);
                input.MeterHeard = false;
            }
            else if (!listed && _meterOrder.Remove(input))
            {
                input.MeterHeard = false;
            }
        }

        private void InitializeMeters()
        {
            _meterOrder.Clear();
            foreach (var input in _inputs)
            {
                TrackMeter(input);
            }
        }

        // The magnitude is what the samples are on average, the peak is above it, and what is said is those with the volume applied (and the peak of the input as it is).
        private float[][] NextLevels(SimInput input)
        {
            var volume = input.Muted ? 0f : input.Volume;
            var channels = new float[2][];
            for (var channel = 0; channel < channels.Length; channel++)
            {
                var magnitude = (float)(_obs.Random.NextDouble() * 0.7);
                var peak = magnitude + ((float)_obs.Random.NextDouble() * (1f - magnitude));
                channels[channel] = [magnitude * volume, peak * volume, peak];
            }

            return channels;
        }

        // What is wrong with the list of the meters: an input that is on the air and has audio and is not in it, or one that is in it and is not on the air, or one that is in it twice.
        private IEnumerable<string> FindMeterProblems()
        {
            foreach (var input in _inputs)
            {
                var listed = _meterOrder.Count(known => ReferenceEquals(known, input));
                var wanted = input.Active && input.Kind.HasAudio ? 1 : 0;
                if (listed != wanted)
                {
                    yield return $"Input \"{input.Name}\" is listed {listed} times in the volume meters, and it is {(input.Active ? "on the air" : "off the air")} and {(input.Kind.HasAudio ? "has audio" : "has no audio")}.";
                }
            }
        }
    }
}
