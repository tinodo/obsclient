namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    public class InputActiveStateChangedEventArgs : EventArgs
    {
        [JsonPropertyName("inputName")]
        public string InputName { get; }

        [JsonPropertyName("videoActive")]
        public bool VideoActive { get; }

        [JsonConstructor]
        public InputActiveStateChangedEventArgs(string inputName, bool videoActive)
        {
            this.InputName = inputName;
            this.VideoActive = videoActive;
        }
    }
}
