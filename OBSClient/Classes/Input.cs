namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;

    public class Input
    {
        [JsonPropertyName("inputKind")]
        public string InputKind { get; }

        [JsonPropertyName("inputName")]
        public string InputName { get; }

        [JsonPropertyName("unversionedInputKind")]
        public string UnversionedInputKind { get; }

        [JsonConstructor]
        public Input(string inputKind, string inputName, string unversionedInputKind)
        {
            InputKind = inputKind;
            InputName = inputName;
            UnversionedInputKind = unversionedInputKind;
        }
    }
}
