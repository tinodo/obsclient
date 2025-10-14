namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the InputMuteStateChanged event.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="InputMuteStateChangedEventArgs"/> class.
    /// </remarks>
    /// <param name="inputName">The input name.</param>
    /// <param name="inputUuid">The input uuid.</param>
    /// <param name="inputMuted">A value indicating whether the input is muted.</param>
    [method: JsonConstructor]
    public class InputMuteStateChangedEventArgs(string inputName, Guid inputUuid, bool inputMuted) : EventArgs
    {
        /// <summary>
        /// Gets the input name.
        /// </summary>
        [JsonPropertyName("inputName")]
        public string InputName { get; } = inputName;

        /// <summary>
        /// Gets the input uuid.
        /// </summary>
        [JsonPropertyName("inputUuid")]
        public Guid InputUuid { get; } = inputUuid;

        /// <summary>
        /// Gets a value indicating whether the input is muted.
        /// </summary>
        [JsonPropertyName("inputMuted")]
        public bool InputMuted { get; } = inputMuted;
    }
}
