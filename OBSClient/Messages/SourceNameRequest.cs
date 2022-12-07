namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;

    public class SourceNameRequest
    {
        [JsonPropertyName("sourceName")]
        public string SourceName { get; set; }

        [JsonConstructor]
        public SourceNameRequest(string sourceName)
        {
            this.SourceName = sourceName;
        }
    }
}
