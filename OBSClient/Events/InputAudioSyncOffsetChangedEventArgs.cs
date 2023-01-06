namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the InputAudioSyncOffsetChanged event.
    /// </summary>
    public class InputAudioSyncOffsetChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the name of the input.
        /// </summary>
        [JsonPropertyName("inputName")]
        public string InputName { get; }

        /// <summary>
        /// Gets the audio sync offset of the input.
        /// </summary>
        [JsonPropertyName("inputAudioSyncOffset")]
        public int InputAudioSyncOffset { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="InputAudioSyncOffsetChangedEventArgs"/> class.
        /// </summary>
        /// <param name="inputName">The name of the input.</param>
        /// <param name="inputAudioSyncOffset">The audio offset of the input.</param>
        [JsonConstructor]
        public InputAudioSyncOffsetChangedEventArgs(string inputName, int inputAudioSyncOffset)
        {
            this.InputName = inputName;
            this.InputAudioSyncOffset = inputAudioSyncOffset;
        }
    }
}
