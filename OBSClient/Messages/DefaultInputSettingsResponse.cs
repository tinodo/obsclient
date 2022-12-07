namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class DefaultInputSettingsResponse : IResponse
    {
        [JsonPropertyName("defaultInputSettings")]
        public Dictionary<string, object> DefaultInputSettings { get; set; }

        [JsonConstructor]
        public DefaultInputSettingsResponse(Dictionary<string, object> defaultInputSettings)
        {
            this.DefaultInputSettings = defaultInputSettings ?? new();
        }
    }
}
