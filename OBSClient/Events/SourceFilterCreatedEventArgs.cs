namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Classes;

    public class SourceFilterCreatedEventArgs : EventArgs
    {
        [JsonPropertyName("sourceName")]
        public string SourceName { get; }

        [JsonPropertyName("filterName")]
        public string FilterName { get; }

        [JsonPropertyName("filterKind")]
        public string FilterKind { get; }

        [JsonPropertyName("filterIndex")]
        public int FilterIndex { get; }

        [JsonPropertyName("filterSettings")]
        public FilterSettings FilterSettings { get; }

        [JsonPropertyName("defaultFilterSettings")]
        public Dictionary<string, object> DefaultFilterSettings { get; }

        [JsonConstructor]
        public SourceFilterCreatedEventArgs(string sourceName, string filterName, string filterKind, int filterIndex, FilterSettings filterSettings, Dictionary<string, object> defaultFilterSettings)
        {
            this.SourceName = sourceName;
            this.FilterName = filterName;
            this.FilterKind = filterKind;
            this.FilterIndex = filterIndex;
            this.FilterSettings = filterSettings;
            this.DefaultFilterSettings = defaultFilterSettings;
        }
    }
}
