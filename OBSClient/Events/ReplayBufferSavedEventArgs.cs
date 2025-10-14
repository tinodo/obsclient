namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the ReplayBufferSaved event.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="ReplayBufferSavedEventArgs"/> class.
    /// </remarks>
    /// <param name="savedReplayPath">The path for the saved replay.</param>
    [method: JsonConstructor]
    public class ReplayBufferSavedEventArgs(string savedReplayPath) : EventArgs
    {
        /// <summary>
        /// Gets the path for the saved replay.
        /// </summary>
        [JsonPropertyName("savedReplayPath")]
        public string SavedReplayPath { get; } = savedReplayPath;
    }
}
