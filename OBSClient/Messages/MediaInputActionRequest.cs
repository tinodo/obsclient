namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Enums;
    using System.Text.Json.Serialization;

    public class MediaInputActionRequest
    {
        [JsonPropertyName("inputName")]
        public string InputName { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("mediaAction")]
        public ObsMediaInputAction MediaAction { get; set; }

        public MediaInputActionRequest(string inputName, ObsMediaInputAction mediaAction)
        {
            this.InputName = inputName;
            this.MediaAction = mediaAction;
        }
    }
}
