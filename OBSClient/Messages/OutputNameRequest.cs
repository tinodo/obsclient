namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;

    public class OutputNameRequest
    {
        [JsonPropertyName("outputName")]
        public string OutputName { get; set; }

        [JsonConstructor]
        public OutputNameRequest(string outputName)
        {
            this.OutputName = outputName;
        }
    }
}
