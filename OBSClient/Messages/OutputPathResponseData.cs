namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class OutputPathResponseData : IResponseData
    {
        [JsonPropertyName("outputPath")]
        public string OutputPath { get; set; }

        [JsonConstructor]
        public OutputPathResponseData(string outputPath)
        {
            this.OutputPath = outputPath;
        }
    }
}
