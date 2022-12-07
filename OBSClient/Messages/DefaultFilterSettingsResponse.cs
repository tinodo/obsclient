namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class DefaultFilterSettingsResponse : IResponse
    {
        [JsonPropertyName("defaultFilterSettings")]
        public Dictionary<string, object> DefaultFilterSettings { get;set; }

        [JsonConstructor]
        public DefaultFilterSettingsResponse(Dictionary<string, object> defaultFilterSettings)
        {
            this.DefaultFilterSettings = defaultFilterSettings ?? new();
        }
    }
}
