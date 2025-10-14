namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the InputAudioSyncOffsetChanged event.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="InputAudioSyncOffsetChangedEventArgs"/> class.
    /// </remarks>
    /// <param name="inputName">The name of the input.</param>
    /// <param name="inputUuid">The uuid of the input.</param>
    /// <param name="inputAudioSyncOffset">The audio offset of the input.</param>
    [method: JsonConstructor]
    public class InputAudioSyncOffsetChangedEventArgs(string inputName, Guid inputUuid, int inputAudioSyncOffset) : EventArgs
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
        /// Gets the audio sync offset of the input.
        /// </summary>
        [JsonPropertyName("inputAudioSyncOffset")]
        public int InputAudioSyncOffset { get; } = inputAudioSyncOffset;
    }
}
