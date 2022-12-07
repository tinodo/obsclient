namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class InputKindsResponseData : IResponseData
    {
        [JsonPropertyName("inputKinds")]
        public string[] InputKinds { get; set; }

        public InputKindsResponseData(string[] inputKinds)
        {
            this.InputKinds = inputKinds ?? Array.Empty<string>();
        }
    }
}
