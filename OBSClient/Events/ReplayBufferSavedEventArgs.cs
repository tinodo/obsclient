namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the ReplayBufferSaved event.
    /// </summary>
    public class ReplayBufferSavedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the path for the saved replay.
        /// </summary>
        [JsonPropertyName("savedReplayPath")]
        public string SavedReplayPath { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ReplayBufferSavedEventArgs"/> class.
        /// </summary>
        /// <param name="savedReplayPath">The path for the saved replay.</param>
        [JsonConstructor]
        public ReplayBufferSavedEventArgs(string savedReplayPath)
        {
            this.SavedReplayPath = savedReplayPath;
        }
    }
}
