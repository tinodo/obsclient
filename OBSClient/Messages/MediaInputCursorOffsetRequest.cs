namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;

    public class MediaInputCursorOffsetRequest
    {
        [JsonPropertyName("inputName")]
        public string InputName { get; set; }

        [JsonPropertyName("mediaCursorOffset")]
        public long MediaCursorOffset { get; set; }

        public MediaInputCursorOffsetRequest(string inputName, long mediaCursorOffset)
        {
            this.InputName = inputName;
            this.MediaCursorOffset = mediaCursorOffset;
        }
    }
}
