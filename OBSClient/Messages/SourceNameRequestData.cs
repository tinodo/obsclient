namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;

    public class SourceNameRequestData
    {
        [JsonPropertyName("sourceName")]
        public string SourceName { get; set; }

        [JsonConstructor]
        public SourceNameRequestData(string sourceName)
        {
            this.SourceName = sourceName;
        }
    }
}
