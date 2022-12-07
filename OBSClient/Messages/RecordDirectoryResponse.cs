namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class RecordDirectoryResponse : IResponse
    {
        [JsonPropertyName("recordDirectory")]
        public string RecordDirectory { get; set; }

        [JsonConstructor]
        public RecordDirectoryResponse(string recordDirectory)
        {
            this.RecordDirectory = recordDirectory;
        }
    }
}
