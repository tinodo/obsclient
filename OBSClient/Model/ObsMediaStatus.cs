namespace OBSStudioClient.Model
{
    /// <summary>
    /// What OBS Studio says about a media input at the moment that it was asked (see <see cref="ObsMedia.GetStatusAsync"/>): a snapshot, that does not change.
    /// </summary>
    public sealed class ObsMediaStatus
    {
        internal ObsMediaStatus(ObsMediaState state, TimeSpan? duration, TimeSpan? position)
        {
            this.State = state;
            this.Duration = duration;
            this.Position = position;
        }

        /// <summary>
        /// Gets what the media is doing. This is what OBS Studio holds at the moment of the answer: it can be a state that <see cref="ObsMedia.State"/> does not show
        /// (<see cref="ObsMediaState.Opening"/>, <see cref="ObsMediaState.Buffering"/> or <see cref="ObsMediaState.Error"/>).
        /// </summary>
        public ObsMediaState State { get; }

        /// <summary>
        /// Gets how long the media is, or null when OBS Studio does not say: it only knows while the media plays or is paused. A media file that cannot be played says a length of zero.
        /// </summary>
        public TimeSpan? Duration { get; }

        /// <summary>
        /// Gets where the media is (the position of the cursor), or null when OBS Studio does not say: it only knows while the media plays or is paused. A media input that is not on the program
        /// output does not move, and keeps saying the position that it had.
        /// </summary>
        public TimeSpan? Position { get; }

        /// <inheritdoc/>
        public override string ToString()
        {
            return this.Duration is { } duration && this.Position is { } position ? $"{this.State}, {position:g} of {duration:g}" : this.State.ToString();
        }
    }
}
