namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the InputActiveStateChanged event.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="InputActiveStateChangedEventArgs"/> class.
    /// </remarks>
    /// <param name="inputName">The name of the input.</param>
    /// <param name="inputUuid">The uuid of the input.</param>
    /// <param name="videoActive">A value indicating whether the video is active or not.</param>
    [method: JsonConstructor]
    public class InputActiveStateChangedEventArgs(string inputName, Guid inputUuid, bool videoActive) : EventArgs
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
        /// Gets a value indicating whether the video is active or not.
        /// </summary>
        [JsonPropertyName("videoActive")]
        public bool VideoActive { get; } = videoActive;
    }
}
