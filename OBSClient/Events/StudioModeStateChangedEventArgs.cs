namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the StudioModeStateChanged event.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="StudioModeStateChangedEventArgs"/> class.
    /// </remarks>
    /// <param name="studioModeEnabled">A value indicating whether Studio Mode is enabled.</param>
    [method: JsonConstructor]
    public class StudioModeStateChangedEventArgs(bool studioModeEnabled) : EventArgs
    {
        /// <summary>
        /// Gets a value indicating whether Studio Mode is enabled.
        /// </summary>
        [JsonPropertyName("studioModeEnabled")]
        public bool StudioModeEnabled { get; } = studioModeEnabled;
    }
}
