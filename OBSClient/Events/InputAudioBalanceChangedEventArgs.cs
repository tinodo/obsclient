namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    public class InputAudioBalanceChangedEventArgs : EventArgs
    {
        [JsonPropertyName("inputName")]
        public string InputName { get; }

        [JsonPropertyName("inputAudioBalance")]
        public float InputAudioBalance { get; }

        [JsonConstructor]
        public InputAudioBalanceChangedEventArgs(string inputName, float inputAudioBalance)
        {
            this.InputName = inputName;
            this.InputAudioBalance = inputAudioBalance;
        }
    }
}
