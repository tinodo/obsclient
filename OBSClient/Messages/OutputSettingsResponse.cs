namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class OutputSettingsResponse : IResponse
    {
        [JsonPropertyName("outputSettings")]
        public Dictionary<string, object> OutputSettings { get; set; }

        [JsonConstructor]
        public OutputSettingsResponse(Dictionary<string, object> outputSettings)
        {
            this.OutputSettings = outputSettings;
        }
    }
}
