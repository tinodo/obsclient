namespace OBSStudioClient.Events
{
    using OBSStudioClient.Classes;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the InputAudioTracksChanged event.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="InputAudioTracksChangedEventArgs"/> class.
    /// </remarks>
    /// <param name="inputName">The name of the input.</param>
    /// <param name="inputUuid">The uuid of the input.</param>
    /// <param name="inputAudioTracks">The audio tracks for the input.</param>
    [method: JsonConstructor]
    public class InputAudioTracksChangedEventArgs(string inputName, Guid inputUuid, AudioTracks inputAudioTracks) : EventArgs
    {
        /// <summary>
        /// Gets the name of the input.
        /// </summary>
        [JsonPropertyName("inputName")]
        public string InputName { get; } = inputName;

        /// <summary>
        /// Gets the input uuid.
        /// </summary>
        [JsonPropertyName("inputUuid")]
        public Guid InputUuid { get; } = inputUuid;

        /// <summary>
        /// Gets the <see cref="AudioTracks"/> for the input.
        /// </summary>
        [JsonPropertyName("inputAudioTracks")]
        public AudioTracks InputAudioTracks { get; } = inputAudioTracks;
    }
}
