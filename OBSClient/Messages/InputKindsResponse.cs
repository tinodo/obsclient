namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class InputKindsResponse : IResponse
    {
        [JsonPropertyName("inputKinds")]
        public string[] InputKinds { get; set; }

        public InputKindsResponse(string[] inputKinds)
        {
            this.InputKinds = inputKinds ?? Array.Empty<string>();
        }
    }
}
