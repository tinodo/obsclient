namespace OBSStudioClient.Model
{
    /// <summary>
    /// What a media input of OBS Studio is doing (see <see cref="ObsMedia.State"/>).
    /// </summary>
    /// <remarks>
    /// <see cref="ObsMedia.State"/> is kept by the events that OBS Studio sends about playback: it becomes <see cref="Playing"/> when OBS Studio is told to play or restart (or says that the playback
    /// started), <see cref="Paused"/> and <see cref="Stopped"/> when it is told to pause or stop, and <see cref="Ended"/> when the playback ended. OBS Studio sends no event for the states in between, so
    /// <see cref="Opening"/>, <see cref="Buffering"/> and <see cref="Error"/> only show when the model reads the state (when it connects, and when the input is made): ask <see cref="ObsMedia.GetStatusAsync"/> to know
    /// what OBS Studio holds now.
    /// </remarks>
    public enum ObsMediaState
    {
        /// <summary>
        /// OBS Studio has no state for the media: it has not opened it. A media source that restarts when it is shown (the default) does not open its file while its scene is not on the program output, until it is
        /// played or paused: that is what an input that was just made, or a restarted OBS Studio, is.
        /// </summary>
        None,

        /// <summary>
        /// The media is playing (as far as OBS Studio can tell: a media that is not shown does not move, and says that it plays).
        /// </summary>
        Playing,

        /// <summary>
        /// OBS Studio is opening the media. Only seen when the model reads the state.
        /// </summary>
        Opening,

        /// <summary>
        /// OBS Studio is waiting for more of the media (a stream). Only seen when the model reads the state.
        /// </summary>
        Buffering,

        /// <summary>
        /// The playback is paused.
        /// </summary>
        Paused,

        /// <summary>
        /// The playback was stopped.
        /// </summary>
        Stopped,

        /// <summary>
        /// The media played to its end (and does not loop). OBS Studio also says so for a file that it could not open, and when the source that shows it stopped being shown.
        /// </summary>
        Ended,

        /// <summary>
        /// OBS Studio could not play the media. Only seen when the model reads the state.
        /// </summary>
        Error,
    }
}
