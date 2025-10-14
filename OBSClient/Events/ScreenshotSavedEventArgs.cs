namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the ScreenshotSaved event.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="ScreenshotSavedEventArgs"/> class.
    /// </remarks>
    /// <param name="savedScreenshotPath">The path to the saved screenshot.</param>
    [method: JsonConstructor]
    public class ScreenshotSavedEventArgs(string savedScreenshotPath) : EventArgs
    {
        /// <summary>
        /// Gets the path to the saved screenshot.
        /// </summary>
        [JsonPropertyName("savedScreenshotPath")]
        public string SavedScreenshotPath { get; } = savedScreenshotPath;
    }
}
