namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class InputMutedResponseData : IResponseData
    {
        [JsonPropertyName("inputMuted")]
        public bool InputMuted { get; set; }

        [JsonConstructor]
        public InputMutedResponseData(bool inputMuted)
        {
            this.InputMuted = inputMuted;
        }
    }
}
