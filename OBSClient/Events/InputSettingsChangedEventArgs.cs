namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the InputSettingsChanged event.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="InputSettingsChangedEventArgs"/> class.
    /// </remarks>
    /// <param name="inputName">The name of the input.</param>
    /// <param name="inputUuid">The uuid of the input.</param>
    /// <param name="inputSettings">The new settings objects of the input.</param>
    [method: JsonConstructor]
    public class InputSettingsChangedEventArgs(string inputName, Guid inputUuid, Dictionary<string, object> inputSettings) : EventArgs
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
        /// Gets the new settings objects of the input.
        /// </summary>
        [JsonPropertyName("inputSettings")]
        public Dictionary<string, object> InputSettings { get; } = inputSettings;
    }
}
