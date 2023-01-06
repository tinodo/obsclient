namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides a class for Input.
    /// </summary>
    public class Input
    {
        /// <summary>
        /// Gets the input kind.
        /// </summary>
        [JsonPropertyName("inputKind")]
        public string InputKind { get; }

        /// <summary>
        /// Gets the input name.
        /// </summary>
        [JsonPropertyName("inputName")]
        public string InputName { get; }

        /// <summary>
        /// Gets the unversioned input kind.
        /// </summary>
        [JsonPropertyName("unversionedInputKind")]
        public string UnversionedInputKind { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Input"/> class.
        /// </summary>
        /// <param name="inputKind">The input kind.</param>
        /// <param name="inputName">The input name.</param>
        /// <param name="unversionedInputKind">The unversioned input kind.</param>
        [JsonConstructor]
        public Input(string inputKind, string inputName, string unversionedInputKind)
        {
            this.InputKind = inputKind;
            this.InputName = inputName;
            this.UnversionedInputKind = unversionedInputKind;
        }
    }
}
