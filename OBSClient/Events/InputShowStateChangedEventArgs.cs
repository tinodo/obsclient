namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the InputShowStateChanged event.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="InputShowStateChangedEventArgs"/> class.
    /// </remarks>
    /// <param name="inputName">The input name.</param>
    /// <param name="inputUuid">The input uuid.</param>
    /// <param name="videoShowing">A value indicating whether the video is showing or not.</param>
    [method: JsonConstructor]
    public class InputShowStateChangedEventArgs(string inputName, Guid inputUuid, bool videoShowing) : EventArgs
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
        /// Gets a value indicating whether the video is showing or not.
        /// </summary>
        [JsonPropertyName("videoShowing")]
        public bool VideoShowing { get; } = videoShowing;
    }
}
