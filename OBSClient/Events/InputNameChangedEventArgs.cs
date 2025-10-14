namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the InputNameChanged event.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="InputNameChangedEventArgs"/> class.
    /// </remarks>
    /// <param name="inputUuid">The input uuid.</param>
    /// <param name="oldInputName">The old input name.</param>
    /// <param name="inputName">The input name.</param>
    [method: JsonConstructor]
    public class InputNameChangedEventArgs(Guid inputUuid, string oldInputName, string inputName) : EventArgs
    {
        /// <summary>
        /// Gets the input uuid.
        /// </summary>
        [JsonPropertyName("inputUuid")]
        public Guid InputUuid { get; } = inputUuid;

        /// <summary>
        /// Gets the old input name.
        /// </summary>
        [JsonPropertyName("oldInputName")]
        public string OldInputName { get; } = oldInputName;

        /// <summary>
        /// Gets the input name.
        /// </summary>
        [JsonPropertyName("inputName")]
        public string InputName { get; } = inputName;
    }
}
