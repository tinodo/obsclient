namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class InputSettingsResponseData : IResponseData
    {
        [JsonPropertyName("inputSettings")]
        public Dictionary<string, object> InputSettings { get; set; }

        [JsonPropertyName("inputKind")]
        public string InputKind { get; set; }

        [JsonConstructor]
        public InputSettingsResponseData(Dictionary<string, object> inputSettings, string inputKind)
        {
            this.InputSettings = inputSettings ?? new();
            this.InputKind = inputKind;
        }
    }
}
