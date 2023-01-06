namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the ScreenshotSaved event.
    /// </summary>
    public class ScreenshotSavedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the path to the saved screenshot.
        /// </summary>
        [JsonPropertyName("savedScreenshotPath")]
        public string SavedScreenshotPath { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ScreenshotSavedEventArgs"/> class.
        /// </summary>
        /// <param name="savedScreenshotPath">The path to the saved screenshot.</param>
        [JsonConstructor]
        public ScreenshotSavedEventArgs(string savedScreenshotPath)
        {
            this.SavedScreenshotPath = savedScreenshotPath;
        }
    }
}
