namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    public class ScreenshotSavedEventArgs : EventArgs
    {
        [JsonPropertyName("savedScreenshotPath")]
        public string SavedScreenshotPath { get; }

        [JsonConstructor]
        public ScreenshotSavedEventArgs(string savedScreenshotPath)
        {
            this.SavedScreenshotPath = savedScreenshotPath;
        }
    }
}
