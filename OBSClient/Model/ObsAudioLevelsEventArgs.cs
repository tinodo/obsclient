namespace OBSStudioClient.Model
{
    using System.Collections.ObjectModel;

    /// <summary>
    /// Describes the levels of the audio that OBS Studio sent (see <see cref="ObsModel.AudioLevels"/>).
    /// </summary>
    public sealed class ObsAudioLevelsEventArgs : EventArgs
    {
        internal ObsAudioLevelsEventArgs(ObsAudioLevels[] levels, ObsInput[] ended)
        {
            this.Levels = new ReadOnlyCollection<ObsAudioLevels>(levels);
            this.Ended = new ReadOnlyCollection<ObsInput>(ended);
        }

        /// <summary>
        /// Gets the levels of every input that OBS Studio listed in its last message, in the order in which it listed them (the inputs that came on the air first are first). The
        /// levels are what <see cref="ObsInput.Levels"/> says of each input.
        /// </summary>
        public IReadOnlyList<ObsAudioLevels> Levels { get; }

        /// <summary>
        /// Gets the inputs that OBS Studio stopped listing since the last time that the event was raised: they went off the air (or were removed), or the connection ended. Their
        /// <see cref="ObsInput.Levels"/> is null now.
        /// </summary>
        public IReadOnlyList<ObsInput> Ended { get; }
    }
}
