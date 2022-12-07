namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    public class ReplayBufferSavedEventArgs : EventArgs
    {
        [JsonPropertyName("savedReplayPath")]
        public string SavedReplayPath { get; }

        [JsonConstructor]
        public ReplayBufferSavedEventArgs(string savedReplayPath)
        {
            this.SavedReplayPath = savedReplayPath;
        }
    }
}
