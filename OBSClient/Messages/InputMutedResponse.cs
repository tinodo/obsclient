namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class InputMutedResponse : IResponse
    {
        [JsonPropertyName("inputMuted")]
        public bool InputMuted { get; set; }

        [JsonConstructor]
        public InputMutedResponse(bool inputMuted)
        {
            this.InputMuted = inputMuted;
        }
    }
}
