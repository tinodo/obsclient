namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    public class SourceFilterEnableStateChangedEventArgs : EventArgs
    {
        [JsonPropertyName("sourceName")]
        public string SourceName { get; }

        [JsonPropertyName("filterName")]
        public string FilterName { get; }

        [JsonPropertyName("filterEnabled")]
        public bool FilterEnabled { get; }

        [JsonConstructor]
        public SourceFilterEnableStateChangedEventArgs(string sourceName, string filterName, bool filterEnabled)
        {
            this.SourceName = sourceName;
            this.FilterName = filterName;
            this.FilterEnabled = filterEnabled;
        }
    }
}
