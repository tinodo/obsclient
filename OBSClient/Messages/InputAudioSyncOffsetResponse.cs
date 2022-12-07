namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class InputAudioSyncOffsetResponse : IResponse
    {
        [JsonPropertyName("inputAudioSyncOffset")]
        public int InputAudioSyncOffset { get; set; }

        public InputAudioSyncOffsetResponse(int inputAudioSyncOffset)
        {
            this.InputAudioSyncOffset = inputAudioSyncOffset;
        }
    }
}
