namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;

    public class OutputSettingsRequest : OutputNameRequest
    {
        [JsonPropertyName("outputSettings")]
        public Dictionary<string, object> OutputSettings { get; set; }

        [JsonConstructor]
        public OutputSettingsRequest(string outputName, Dictionary<string, object> outputSettings) : base(outputName)
        {
            this.OutputSettings = outputSettings;
        }
    }
}
