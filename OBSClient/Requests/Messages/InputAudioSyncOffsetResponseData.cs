namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class InputAudioSyncOffsetResponseData : IResponseData
    {
        [JsonPropertyName("inputAudioSyncOffset")]
        public int InputAudioSyncOffset { get; set; }

        public InputAudioSyncOffsetResponseData(int inputAudioSyncOffset)
        {
            this.InputAudioSyncOffset = inputAudioSyncOffset;
        }
    }
}
