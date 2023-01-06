namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the InputActiveStateChanged event.
    /// </summary>
    public class InputActiveStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the name of the input.
        /// </summary>
        [JsonPropertyName("inputName")]
        public string InputName { get; }

        /// <summary>
        /// Gets a value indicating whether the video is active or not.
        /// </summary>
        [JsonPropertyName("videoActive")]
        public bool VideoActive { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="InputActiveStateChangedEventArgs"/> class.
        /// </summary>
        /// <param name="inputName">The name of the input.</param>
        /// <param name="videoActive">A value indicating whether the video is active or not.</param>
        [JsonConstructor]
        public InputActiveStateChangedEventArgs(string inputName, bool videoActive)
        {
            this.InputName = inputName;
            this.VideoActive = videoActive;
        }
    }
}
