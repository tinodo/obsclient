namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;

    public class InputNameRequest
    {
        [JsonPropertyName("inputName")]
        public string InputName { get; set; }

        [JsonConstructor]
        public InputNameRequest(string inputName)
        {
            this.InputName = inputName;
        }
    }
}
