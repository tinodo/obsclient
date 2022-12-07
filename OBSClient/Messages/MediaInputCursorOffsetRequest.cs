namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;

    public class MediaInputCursorOffsetRequest
    {
        [JsonPropertyName("inputName")]
        public string InputName { get; set; }

        [JsonPropertyName("mediaCursorOffset")]
        public int MediaCursorOffset { get; set; }

        public MediaInputCursorOffsetRequest(string inputName, int mediaCursorOffset)
        {
            this.InputName = inputName;
            this.MediaCursorOffset = mediaCursorOffset;
        }
    }
}
