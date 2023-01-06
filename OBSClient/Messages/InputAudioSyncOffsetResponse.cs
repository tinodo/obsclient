namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetInputAudioSyncOffset request.
    /// </summary>
    public class InputAudioSyncOffsetResponse : IResponse
    {
        /// <summary>
        /// Gets the input's audio sync offset in milliseconds.
        /// </summary>
        [JsonPropertyName("inputAudioSyncOffset")]
        public int InputAudioSyncOffset { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="InputAudioSyncOffsetResponse"/> class.
        /// </summary>
        /// <param name="inputAudioSyncOffset">Audio sync offet in milliseconds.</param>
        public InputAudioSyncOffsetResponse(int inputAudioSyncOffset)
        {
            this.InputAudioSyncOffset = inputAudioSyncOffset;
        }
    }
}
