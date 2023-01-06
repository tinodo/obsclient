namespace OBSStudioClient.Classes
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides a class for AudioTracks.
    /// </summary>
    public class AudioTracks
    {
        /// <summary>
        /// Gets a value indicating whether Audio Track 1 is enabled.
        /// </summary>
        [JsonPropertyName("1")]
        public bool Track1 { get; }

        /// <summary>
        /// Gets a value indicating whether Audio Track 2 is enabled.
        /// </summary>
        [JsonPropertyName("2")]
        public bool Track2 { get; }

        /// <summary>
        /// Gets a value indicating whether Audio Track 3 is enabled.
        /// </summary>
        [JsonPropertyName("3")]
        public bool Track3 { get; }

        /// <summary>
        /// Gets a value indicating whether Audio Track 4 is enabled.
        /// </summary>
        [JsonPropertyName("4")]
        public bool Track4 { get; }

        /// <summary>
        /// Gets a value indicating whether Audio Track 5 is enabled.
        /// </summary>
        [JsonPropertyName("5")]
        public bool Track5 { get; }

        /// <summary>
        /// Gets a value indicating whether Audio Track 6 is enabled.
        /// </summary>
        [JsonPropertyName("6")]
        public bool Track6 { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioTracks"/> class.
        /// </summary>
        /// <param name="track1">A value indicating whether Audio Track 1 is enabled.</param>
        /// <param name="track2">A value indicating whether Audio Track 2 is enabled.</param>
        /// <param name="track3">A value indicating whether Audio Track 3 is enabled.</param>
        /// <param name="track4">A value indicating whether Audio Track 4 is enabled.</param>
        /// <param name="track5">A value indicating whether Audio Track 5 is enabled.</param>
        /// <param name="track6">A value indicating whether Audio Track 6 is enabled.</param>
        [JsonConstructor]
        public AudioTracks(bool track1, bool track2, bool track3, bool track4, bool track5, bool track6)
        {
            this.Track1 = track1;
            this.Track2 = track2;
            this.Track3 = track3;
            this.Track4 = track4;
            this.Track5 = track5;
            this.Track6 = track6;
        }
    }
}
