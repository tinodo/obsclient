namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;

    public class MediaInputCursorRequest
    {
        [JsonPropertyName("inputName")]
        public string InputName { get; set; }

        [JsonPropertyName("mediaCursor")]
        public long MediaCursor { get; set; }

        public MediaInputCursorRequest(string inputName, long mediaCursor)
        {
            this.InputName = inputName;
            this.MediaCursor = mediaCursor;
        }
    }
}
