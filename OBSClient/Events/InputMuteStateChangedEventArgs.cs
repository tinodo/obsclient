namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the InputMuteStateChanged event.
    /// </summary>
    public class InputMuteStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the input name.
        /// </summary>
        [JsonPropertyName("inputName")]
        public string InputName { get; }

        /// <summary>
        /// Gets a value indicating whether the input is muted.
        /// </summary>
        [JsonPropertyName("inputMuted")]
        public bool InputMuted { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="InputMuteStateChangedEventArgs"/> class.
        /// </summary>
        /// <param name="inputName">The input name.</param>
        /// <param name="inputMuted">A value indicating whether the input is muted.</param>
        [JsonConstructor]
        public InputMuteStateChangedEventArgs(string inputName, bool inputMuted)
        {
            this.InputName = inputName;
            this.InputMuted = inputMuted;
        }
    }
}
