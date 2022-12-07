namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class InputSettingsResponse : IResponse
    {
        [JsonPropertyName("inputSettings")]
        public Dictionary<string, object> InputSettings { get; set; }

        [JsonPropertyName("inputKind")]
        public string InputKind { get; set; }

        [JsonConstructor]
        public InputSettingsResponse(Dictionary<string, object> inputSettings, string inputKind)
        {
            this.InputSettings = inputSettings ?? new();
            this.InputKind = inputKind;
        }
    }
}
