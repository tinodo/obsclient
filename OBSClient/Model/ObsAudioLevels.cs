namespace OBSStudioClient.Model
{
    using System.Collections.ObjectModel;

    /// <summary>
    /// The levels of the audio of an input: one <see cref="ObsChannelLevel"/> for each channel, as OBS Studio measured them in its last message (see <see cref="ObsInput.Levels"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is a snapshot: a new message makes a new object, and the one that you hold does not change. OBS Studio lists an input when it is on the air and has audio, and the levels of an input that
    /// it has no audio from yet (it just came on the air, or has no audio source open) have no <see cref="Channels"/>.
    /// </para>
    /// <para>
    /// A mono file is delivered by OBS Studio as two channels with the same levels; the first channel is the left one.
    /// </para>
    /// </remarks>
    public sealed class ObsAudioLevels
    {
        internal ObsAudioLevels(ObsInput input, ObsChannelLevel[] channels)
        {
            this.Input = input;
            this.ChannelArray = channels;
            this.Channels = new ReadOnlyCollection<ObsChannelLevel>(channels);
        }

        /// <summary>
        /// Gets the input that the levels are of.
        /// </summary>
        public ObsInput Input { get; }

        /// <summary>
        /// Gets the levels of the channels, the left one first. Empty while OBS Studio has no audio from the input yet.
        /// </summary>
        public IReadOnlyList<ObsChannelLevel> Channels { get; }

        internal ObsChannelLevel[] ChannelArray { get; }

        // Whether another message says the same: the same number of channels and the same numbers.
        internal bool HasSameLevelsAs(ObsAudioLevels other)
        {
            return this.ChannelArray.AsSpan().SequenceEqual(other.ChannelArray);
        }
    }
}
