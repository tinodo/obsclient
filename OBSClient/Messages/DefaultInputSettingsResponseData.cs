namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class DefaultInputSettingsResponseData : IResponseData
    {
        [JsonPropertyName("defaultInputSettings")]
        public Dictionary<string, object> DefaultInputSettings { get; set; }

        [JsonConstructor]
        public DefaultInputSettingsResponseData(Dictionary<string, object> defaultInputSettings)
        {
            this.DefaultInputSettings = defaultInputSettings ?? new();
        }
    }
}
