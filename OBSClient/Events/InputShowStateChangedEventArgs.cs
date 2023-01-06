namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the InputShowStateChanged event.
    /// </summary>
    public class InputShowStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the name of the input.
        /// </summary>
        [JsonPropertyName("inputName")]
        public string InputName { get; }

        /// <summary>
        /// Gets a value indicating whether the video is showing or not.
        /// </summary>
        [JsonPropertyName("videoShowing")]
        public bool VideoShowing { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="InputShowStateChangedEventArgs"/> class.
        /// </summary>
        /// <param name="inputName">The input name.</param>
        /// <param name="videoShowing">A value indicating whether the video is showing or not.</param>
        [JsonConstructor]
        public InputShowStateChangedEventArgs(string inputName, bool videoShowing)
        {
            this.InputName = inputName;
            this.VideoShowing = videoShowing;
        }
    }
}
