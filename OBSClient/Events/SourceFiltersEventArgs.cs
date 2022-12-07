namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Classes;

    public class SourceFiltersEventArgs : EventArgs
    {
        [JsonPropertyName("sourceName")]
        public string SourceName { get; }

        [JsonPropertyName("filters")]
        public Filter[] Filters { get; }

        [JsonConstructor]
        public SourceFiltersEventArgs(string sourceName, Filter[] filters)
        {
            this.SourceName = sourceName;
            this.Filters = filters ?? Array.Empty<Filter>();
        }
    }
}
