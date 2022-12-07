namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;

    public class MediaInputCursorRequest
    {
        [JsonPropertyName("inputName")]
        public string InputName { get; set; }

        [JsonPropertyName("mediaCursor")]
        public int MediaCursor { get; set; }

        public MediaInputCursorRequest(string inputName, int mediaCursor)
        {
            this.InputName = inputName;
            this.MediaCursor = mediaCursor;
        }
    }
}
