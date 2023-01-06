namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the InputRemoved, MediaInputPlaybackStarted and MediaInputEnded events.
    /// </summary>
    public class InputNameEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the name of the input.
        /// </summary>
        [JsonPropertyName("inputName")]
        public string InputName { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="InputNameEventArgs"/> class.
        /// </summary>
        /// <param name="inputName">The input name.</param>
        [JsonConstructor]
        public InputNameEventArgs(string inputName)
        {
            this.InputName = inputName;
        }
    }
}
