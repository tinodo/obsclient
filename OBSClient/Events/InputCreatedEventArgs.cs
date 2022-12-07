namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    public class InputCreatedEventArgs : EventArgs
    {
        [JsonPropertyName("inputName")]
        public string InputName { get; }

        [JsonPropertyName("inputKind")]
        public string InputKind { get; }

        [JsonPropertyName("unversionedInputKind")]
        public string UnversionedInputKind { get; }

        [JsonPropertyName("inputSettings")]
        public Dictionary<string, object> InputSettings { get; }

        [JsonPropertyName("defaultInputSettings")]
        public Dictionary<string, object> DefaultInputSettings { get; }

        [JsonConstructor]
        public InputCreatedEventArgs(string inputName, string inputKind, string unversionedInputKind, Dictionary<string, object> inputSettings, Dictionary<string, object> defaultInputSettings)
        {
            this.InputName = inputName;
            this.InputKind = inputKind;
            this.UnversionedInputKind = unversionedInputKind;
            this.InputSettings = inputSettings ?? new();
            this.DefaultInputSettings = defaultInputSettings ?? new();
        }
    }
}
