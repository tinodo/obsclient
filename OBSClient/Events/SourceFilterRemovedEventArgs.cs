namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    public class SourceFilterRemovedEventArgs : EventArgs
    {
        [JsonPropertyName("sourceName")]
        public string SourceName { get; }

        [JsonPropertyName("filterName")]
        public string FilterName { get; }

        [JsonConstructor]
        public SourceFilterRemovedEventArgs(string sourceName, string filterName)
        {
            this.SourceName = sourceName;
            this.FilterName = filterName;
        }
    }
}
