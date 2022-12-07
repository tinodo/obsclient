namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class OutputSettingsResponseData : IResponseData
    {
        [JsonPropertyName("outputSettings")]
        public Dictionary<string, object> OutputSettings { get; set; }

        [JsonConstructor]
        public OutputSettingsResponseData(Dictionary<string, object> outputSettings)
        {
            this.OutputSettings = outputSettings;
        }
    }
}
