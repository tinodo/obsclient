namespace OBSStudioClient.Model
{
    /// <summary>
    /// The audio tracks that an input is mixed into. OBS Studio has six tracks; the recording and the stream each take the ones that are chosen for them in its output settings.
    /// </summary>
    [Flags]
    public enum ObsAudioTracks
    {
        /// <summary>
        /// The input is on no track: it is heard on the program output only when it is not muted, and not in any track of a recording.
        /// </summary>
        None = 0,

        /// <summary>
        /// Track 1.
        /// </summary>
        Track1 = 1,

        /// <summary>
        /// Track 2.
        /// </summary>
        Track2 = 2,

        /// <summary>
        /// Track 3.
        /// </summary>
        Track3 = 4,

        /// <summary>
        /// Track 4.
        /// </summary>
        Track4 = 8,

        /// <summary>
        /// Track 5.
        /// </summary>
        Track5 = 16,

        /// <summary>
        /// Track 6.
        /// </summary>
        Track6 = 32,

        /// <summary>
        /// All six tracks, which is what a new input has.
        /// </summary>
        All = Track1 | Track2 | Track3 | Track4 | Track5 | Track6,
    }
}
