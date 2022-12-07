namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;

    public class OutputNameRequestData
    {
        [JsonPropertyName("outputName")]
        public string OutputName { get; set; }

        [JsonConstructor]
        public OutputNameRequestData(string outputName)
        {
            this.OutputName = outputName;
        }
    }
}
