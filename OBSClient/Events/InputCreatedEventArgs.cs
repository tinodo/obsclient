namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the InputCreated event.
    /// </summary>
    public class InputCreatedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the name of the input.
        /// </summary>
        [JsonPropertyName("inputName")]
        public string InputName { get; }

        /// <summary>
        /// Gets the input kind.
        /// </summary>
        [JsonPropertyName("inputKind")]
        public string InputKind { get; }

        /// <summary>
        /// Gets the unversioned input kind.
        /// </summary>
        [JsonPropertyName("unversionedInputKind")]
        public string UnversionedInputKind { get; }

        /// <summary>
        /// Gets the input settings.
        /// </summary>
        [JsonPropertyName("inputSettings")]
        public Dictionary<string, object> InputSettings { get; }

        /// <summary>
        /// Gets the default settings for the input.
        /// </summary>
        [JsonPropertyName("defaultInputSettings")]
        public Dictionary<string, object> DefaultInputSettings { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="InputCreatedEventArgs"/> class.
        /// </summary>
        /// <param name="inputName">The input name.</param>
        /// <param name="inputKind">The input kind.</param>
        /// <param name="unversionedInputKind">The unversioned input kind.</param>
        /// <param name="inputSettings">The input settings.</param>
        /// <param name="defaultInputSettings">The default input setting.</param>
        [JsonConstructor]
        public InputCreatedEventArgs(string inputName, string inputKind, string unversionedInputKind, Dictionary<string, object> inputSettings, Dictionary<string, object> defaultInputSettings)
        {
            this.InputName = inputName;
            this.InputKind = inputKind;
            this.UnversionedInputKind = unversionedInputKind;
            this.InputSettings = inputSettings ?? new();
            this.DefaultInputSettings = defaultInputSettings ?? new();
        }
    }
}
