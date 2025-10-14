namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the InputRemoved, MediaInputPlaybackStarted and MediaInputEnded events.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="InputNameEventArgs"/> class.
    /// </remarks>
    /// <param name="inputName">The input name.</param>
    /// <param name="inputUuid">The input uuid.</param>
    [method: JsonConstructor]
    public class InputNameEventArgs(string inputName, Guid inputUuid) : EventArgs
    {
        /// <summary>
        /// Gets the name of the input.
        /// </summary>
        [JsonPropertyName("inputName")]
        public string InputName { get; } = inputName;

        /// <summary>
        /// Gets the UUID of the input.
        /// </summary>
        [JsonPropertyName("inputUuid")]
        public Guid InputUuid { get; } = inputUuid;
    }
}
