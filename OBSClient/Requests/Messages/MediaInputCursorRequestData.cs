namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;

    public class MediaInputCursorRequestData
    {
        [JsonPropertyName("inputName")]
        public string InputName { get; set; }

        [JsonPropertyName("mediaCursor")]
        public int MediaCursor { get; set; }

        public MediaInputCursorRequestData(string inputName, int mediaCursor)
        {
            this.InputName = inputName;
            this.MediaCursor = mediaCursor;
        }
    }
}
