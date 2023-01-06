namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the InputNameChanged event.
    /// </summary>
    public class InputNameChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the old input name.
        /// </summary>
        [JsonPropertyName("oldInputName")]
        public string OldInputName { get; }

        /// <summary>
        /// Gets the input name.
        /// </summary>
        [JsonPropertyName("inputName")]
        public string InputName { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="InputNameChangedEventArgs"/> class.
        /// </summary>
        /// <param name="oldInputName">The old input name.</param>
        /// <param name="inputName">The input name.</param>
        [JsonConstructor]
        public InputNameChangedEventArgs(string oldInputName, string inputName)
        {
            this.OldInputName = oldInputName;
            this.InputName = inputName;
        }
    }
}
