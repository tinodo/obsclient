namespace OBSStudioClient.Events
{
    using OBSStudioClient.Classes;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the InputAudioTracksChanged event.
    /// </summary>
    public class InputAudioTracksChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the name of the input.
        /// </summary>
        [JsonPropertyName("inputName")]
        public string InputName { get; }

        /// <summary>
        /// Gets the <see cref="AudioTracks"/> for the input.
        /// </summary>
        [JsonPropertyName("inputAudioTracks")]
        public AudioTracks InputAudioTracks { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="InputAudioTracksChangedEventArgs"/> class.
        /// </summary>
        /// <param name="inputName">The name of the input.</param>
        /// <param name="inputAudioTracks">The audio tracks for the input.</param>
        [JsonConstructor]
        public InputAudioTracksChangedEventArgs(string inputName, AudioTracks inputAudioTracks)
        {
            this.InputName = inputName;
            this.InputAudioTracks = inputAudioTracks;
        }
    }
}
