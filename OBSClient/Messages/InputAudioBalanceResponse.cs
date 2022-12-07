namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class InputAudioBalanceResponse : IResponse
    {
        [JsonPropertyName("inputAudioBalance")]
        public float InputAudioBalance { get; set; }

        public InputAudioBalanceResponse(float inputAudioBalance)
        {
            this.InputAudioBalance = inputAudioBalance;
        }
    }
}
