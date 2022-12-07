namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    public class StudioModeStateChangedEventArgs : EventArgs
    {
        [JsonPropertyName("studioModeEnabled")]
        public bool StudioModeEnabled { get; }

        [JsonConstructor]
        public StudioModeStateChangedEventArgs(bool studioModeEnabled)
        {
            this.StudioModeEnabled = studioModeEnabled;
        }
    }
}
