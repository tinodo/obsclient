namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;

    public class InputNameRequestData
    {
        [JsonPropertyName("inputName")]
        public string InputName { get; set; }

        [JsonConstructor]
        public InputNameRequestData(string inputName)
        {
            this.InputName = inputName;
        }
    }
}
