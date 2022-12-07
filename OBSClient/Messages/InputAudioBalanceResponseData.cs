namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class InputAudioBalanceResponseData : IResponseData
    {
        [JsonPropertyName("inputAudioBalance")]
        public float InputAudioBalance { get; set; }

        public InputAudioBalanceResponseData(float inputAudioBalance)
        {
            this.InputAudioBalance = inputAudioBalance;
        }
    }
}
