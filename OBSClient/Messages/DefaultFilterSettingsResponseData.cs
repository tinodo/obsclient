namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class DefaultFilterSettingsResponseData : IResponseData
    {
        [JsonPropertyName("defaultFilterSettings")]
        public Dictionary<string, object> DefaultFilterSettings { get;set; }

        [JsonConstructor]
        public DefaultFilterSettingsResponseData(Dictionary<string, object> defaultFilterSettings)
        {
            this.DefaultFilterSettings = defaultFilterSettings ?? new();
        }
    }
}
