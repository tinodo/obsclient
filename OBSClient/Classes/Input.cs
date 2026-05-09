namespace OBSStudioClient.Classes
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides a class for Input.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="Input"/> class.
    /// </remarks>
    /// <param name="inputKind">The input kind.</param>
    /// <param name="inputName">The input name.</param>
    /// <param name="unversionedInputKind">The unversioned input kind.</param>
    /// <summary>
    /// Provides a class for Input.
    /// </summary>
    [method: JsonConstructor]
    public class Input(string inputKind, string inputName, string unversionedInputKind)
    {
        /// <summary>
        /// Gets the input kind.
        /// </summary>
        [JsonPropertyName("inputKind")]
        public string InputKind { get; } = inputKind;

        /// <summary>
        /// Gets the input name.
        /// </summary>
        [JsonPropertyName("inputName")]
        public string InputName { get; } = inputName;

        /// <summary>
        /// Gets the unversioned input kind.
        /// </summary>
        [JsonPropertyName("unversionedInputKind")]
        public string UnversionedInputKind { get; } = unversionedInputKind;
    }
}