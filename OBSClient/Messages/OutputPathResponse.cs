namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class OutputPathResponse : IResponse
    {
        [JsonPropertyName("outputPath")]
        public string OutputPath { get; set; }

        [JsonConstructor]
        public OutputPathResponse(string outputPath)
        {
            this.OutputPath = outputPath;
        }
    }
}
