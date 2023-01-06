namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the StudioModeStateChanged event.
    /// </summary>
    public class StudioModeStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets a value indicating whether Studio Mode is enabled.
        /// </summary>
        [JsonPropertyName("studioModeEnabled")]
        public bool StudioModeEnabled { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="StudioModeStateChangedEventArgs"/> class.
        /// </summary>
        /// <param name="studioModeEnabled">A value indicating whether Studio Mode is enabled.</param>
        [JsonConstructor]
        public StudioModeStateChangedEventArgs(bool studioModeEnabled)
        {
            this.StudioModeEnabled = studioModeEnabled;
        }
    }
}
