namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the InputCreated event.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="InputCreatedEventArgs"/> class.
    /// </remarks>
    /// <param name="inputName">The input name.</param>
    /// <param name="inputUuid">The input uuid.</param>
    /// <param name="inputKind">The input kind.</param>
    /// <param name="unversionedInputKind">The unversioned input kind.</param>
    /// <param name="inputSettings">The input settings.</param>
    /// <param name="defaultInputSettings">The default input setting.</param>
    [method: JsonConstructor]
    public class InputCreatedEventArgs(string inputName, Guid inputUuid, string inputKind, string unversionedInputKind, Dictionary<string, object> inputSettings, Dictionary<string, object> defaultInputSettings) : EventArgs
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
        /// Gets the input kind.
        /// </summary>
        [JsonPropertyName("inputKind")]
        public string InputKind { get; } = inputKind;

        /// <summary>
        /// Gets the unversioned input kind.
        /// </summary>
        [JsonPropertyName("unversionedInputKind")]
        public string UnversionedInputKind { get; } = unversionedInputKind;

        /// <summary>
        /// Gets the input settings.
        /// </summary>
        [JsonPropertyName("inputSettings")]
        public Dictionary<string, object> InputSettings { get; } = inputSettings ?? [];

        /// <summary>
        /// Gets the default settings for the input.
        /// </summary>
        [JsonPropertyName("defaultInputSettings")]
        public Dictionary<string, object> DefaultInputSettings { get; } = defaultInputSettings ?? [];
    }
}
