namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;

    public class OutputSettingsRequestData : OutputNameRequestData
    {
        [JsonPropertyName("outputSettings")]
        public Dictionary<string, object> OutputSettings { get; set; }

        [JsonConstructor]
        public OutputSettingsRequestData(string outputName, Dictionary<string, object> outputSettings) : base(outputName)
        {
            this.OutputSettings = outputSettings;
        }
    }
}
