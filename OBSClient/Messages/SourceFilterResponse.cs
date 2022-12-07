namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Classes;
    using OBSStudioClient.Interfaces;

    public class SourceFilterResponse : IResponse
    {
        [JsonPropertyName("filterEnabled")]
        public bool FilterEnabled { get; set; }

        [JsonPropertyName("filterIndex")]
        public int FilterIndex { get; set; }

        [JsonPropertyName("filterKind")]
        public string FilterKind { get; set; }


        [JsonPropertyName("filterSettings")]
        public FilterSettings FilterSettings { get; set; }

        public SourceFilterResponse(bool filterEnabled, int filterIndex, string filterKind, FilterSettings filterSettings)
        {
            this.FilterEnabled = filterEnabled;
            this.FilterIndex = filterIndex;
            this.FilterKind = filterKind;
            this.FilterSettings = filterSettings;
        }
    }
}
