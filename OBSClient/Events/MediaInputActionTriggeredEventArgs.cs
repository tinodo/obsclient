namespace OBSStudioClient.Events
{
    using OBSStudioClient.Enums;
    using System.Text.Json.Serialization;

    public class MediaInputActionTriggeredEventArgs : EventArgs
    {
        [JsonPropertyName("inputName")]
        public string InputName { get; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("mediaAction")]
        public ObsMediaInputAction MediaAction { get; }

        [JsonConstructor]
        public MediaInputActionTriggeredEventArgs(string inputName, ObsMediaInputAction mediaAction)
        {
            this.InputName = inputName;
            this.MediaAction = mediaAction;
        }
    }
}
