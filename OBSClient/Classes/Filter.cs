namespace OBSStudioClient.Classes
{
    using System.Text.Json.Serialization;

    public class Filter
    {
        [JsonPropertyName("filterEnabled")]
        public bool FilterEnabled { get; }

        [JsonPropertyName("filterIndex")]
        public int FilterIndex { get; }

        [JsonPropertyName("filterKind")]
        public string FilterKind { get; }

        [JsonPropertyName("filterName")]
        public string FilterName { get; }

        [JsonPropertyName("filterSettings")]
        public FilterSettings FilterSettings { get; }

        public Filter(bool filterEnabled, int filterIndex, string filterKind, string filterName, FilterSettings filterSettings)
        {
            FilterEnabled = filterEnabled;
            FilterIndex = filterIndex;
            FilterKind = filterKind;
            FilterName = filterName;
            FilterSettings = filterSettings;
        }
    }
}
