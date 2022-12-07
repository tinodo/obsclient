namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    public class InputAudioSyncOffsetChangedEventArgs : EventArgs
    {
        [JsonPropertyName("inputName")]
        public string InputName { get; }

        [JsonPropertyName("inputAudioSyncOffset")]
        public int InputAudioSyncOffset { get; }

        [JsonConstructor]
        public InputAudioSyncOffsetChangedEventArgs(string inputName, int inputAudioSyncOffset)
        {
            this.InputName = inputName;
            this.InputAudioSyncOffset = inputAudioSyncOffset;
        }
    }
}
