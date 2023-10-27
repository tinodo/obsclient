namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful request of any of these types:
    /// GetVirtualCamStatus, ToggleVirtualCam, ToggleRecord, GetReplayBufferStatus, ToggleReplayBuffer, ToggleOutput and ToggleStream.
    /// </summary>
    public class OutputActiveResponse : IResponse
    {
        /// <summary>
        /// Gets a value indicating whether the output is active.
        /// </summary>
        [JsonPropertyName("outputActive")]
        public bool OutputActive { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OutputActiveResponse"/> class.
        /// </summary>
        /// <param name="outputActive">a value indicating whether the output is active.</param>
        [JsonConstructor]
        public OutputActiveResponse(bool outputActive)
        {
            this.OutputActive = outputActive;
        }
    }
}
