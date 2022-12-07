namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    public class SourceFilterNameChangedEventArgs : EventArgs
    {
        [JsonPropertyName("sourceName")]
        public string SourceName { get; }

        [JsonPropertyName("oldFilterName")]
        public string OldFilterName { get; }

        [JsonPropertyName("filterName")]
        public string FilterName { get; }

        [JsonConstructor]
        public SourceFilterNameChangedEventArgs(string sourceName, string oldFilterName, string filterName)
        {
            this.SourceName = sourceName;
            this.OldFilterName = oldFilterName;
            this.FilterName = filterName;
        }
    }
}
