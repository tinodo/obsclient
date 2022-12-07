namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;

    public class CaptionTextRequest
    {
        [JsonPropertyName("captionText")]
        public string CaptionText { get; set; }

        [JsonConstructor]
        public CaptionTextRequest(string captionText)
        {
            this.CaptionText = captionText;
        }
    }
}
