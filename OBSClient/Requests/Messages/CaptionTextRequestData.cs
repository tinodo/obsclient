namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;

    public class CaptionTextRequestData
    {
        [JsonPropertyName("captionText")]
        public string CaptionText { get; set; }

        [JsonConstructor]
        public CaptionTextRequestData(string captionText)
        {
            this.CaptionText = captionText;
        }
    }
}
