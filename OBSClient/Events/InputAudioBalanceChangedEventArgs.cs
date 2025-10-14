namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the InputAudioBalanceChanged event.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="InputActiveStateChangedEventArgs"/> class.
    /// </remarks>
    /// <param name="inputName">The name of the input.</param>
    /// <param name="inputUuid">The uuid of the input.</param>
    /// <param name="inputAudioBalance">The input audio balance.</param>
    [method: JsonConstructor]
    public class InputAudioBalanceChangedEventArgs(string inputName, Guid inputUuid, float inputAudioBalance) : EventArgs
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
        /// Gets the input audio balance.
        /// </summary>
        [JsonPropertyName("inputAudioBalance")]
        public float InputAudioBalance { get; } = inputAudioBalance;
    }
}
