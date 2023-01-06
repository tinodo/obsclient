namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the InputAudioBalanceChanged event.
    /// </summary>
    public class InputAudioBalanceChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the name of the input.
        /// </summary>
        [JsonPropertyName("inputName")]
        public string InputName { get; }

        /// <summary>
        /// Gets the input audio balance.
        /// </summary>
        [JsonPropertyName("inputAudioBalance")]
        public float InputAudioBalance { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="InputActiveStateChangedEventArgs"/> class.
        /// </summary>
        /// <param name="inputName">The name of the input.</param>
        /// <param name="inputAudioBalance">The input audio balance.</param>
        [JsonConstructor]
        public InputAudioBalanceChangedEventArgs(string inputName, float inputAudioBalance)
        {
            this.InputName = inputName;
            this.InputAudioBalance = inputAudioBalance;
        }
    }
}
