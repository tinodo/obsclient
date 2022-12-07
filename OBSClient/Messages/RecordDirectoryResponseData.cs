namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class RecordDirectoryResponseData : IResponseData
    {
        [JsonPropertyName("recordDirectory")]
        public string RecordDirectory { get; set; }

        [JsonConstructor]
        public RecordDirectoryResponseData(string recordDirectory)
        {
            this.RecordDirectory = recordDirectory;
        }
    }
}
