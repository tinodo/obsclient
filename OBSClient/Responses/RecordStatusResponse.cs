namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetRecordStatus request.
    /// </summary>
    public class RecordStatusResponse : IResponse
    {
        /// <summary>
        /// Gets a value indicating whether the output is active.
        /// </summary>
        [JsonPropertyName("outputActive")]
        public bool OutputActive { get; }

        /// <summary>
        /// Gets a value indicating whether the output is paused.
        /// </summary>
        [JsonPropertyName("outputPaused")]
        public bool OutputPaused { get; }

        /// <summary>
        /// Gets the formatted timecode string of the output.
        /// </summary>
        [JsonPropertyName("outputTimecode")]
        public string OutputTimecode { get; }

        /// <summary>
        /// Gets the duration of the output in milliseconds.
        /// </summary>
        [JsonPropertyName("outputDuration")]
        public long OutputDuration { get; }

        /// <summary>
        /// Gets the number of bytes sent by the output.
        /// </summary>
        [JsonPropertyName("outputBytes")]
        public long OutputBytes { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RecordStatusResponse"/> class.
        /// </summary>
        /// <param name="outputActive">The value indicating whether the output is active.</param>
        /// <param name="outputPaused">The value indicating whether the output is paused.</param>
        /// <param name="outputTimecode">The timecode string of the output.</param>
        /// <param name="outputDuration">The duration of the output in milliseconds.</param>
        /// <param name="outputBytes">The number of bytes sent by the output.</param>
        [JsonConstructor]
        public RecordStatusResponse(bool outputActive, bool outputPaused, string outputTimecode, long outputDuration, long outputBytes)
        {
            this.OutputActive = outputActive;
            this.OutputPaused = outputPaused;
            this.OutputTimecode = outputTimecode;
            this.OutputDuration = outputDuration;
            this.OutputBytes = outputBytes;
        }
    }
}
