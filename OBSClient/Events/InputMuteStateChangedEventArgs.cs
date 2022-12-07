namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    public class InputMuteStateChangedEventArgs : EventArgs
    {
        [JsonPropertyName("inputName")]
        public string InputName { get; }

        [JsonPropertyName("inputMuted")]
        public bool InputMuted { get; }

        [JsonConstructor]
        public InputMuteStateChangedEventArgs(string inputName, bool inputMuted)
        {
            this.InputName = inputName;
            this.InputMuted = inputMuted;
        }
    }
}
