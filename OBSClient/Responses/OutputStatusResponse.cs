namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Converters;
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetOutputStatus or GetStreamStatus request.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="OutputStatusResponse"/> class.
    /// </remarks>
    /// <param name="outputActive">The value indicating whether the output is active.</param>
    /// <param name="outputReconnecting">The value indicating whether the output is reconnecting.</param>
    /// <param name="outputTimecode">The timecode string of the output.</param>
    /// <param name="outputDuration">The duration of the output in milliseconds.</param>
    /// <param name="outputCongestion">The congestion of the output.</param>
    /// <param name="outputBytes">The number of bytes sent by the output.</param>
    /// <param name="outputSkippedFrames">The number of frames skipped by the output's process.</param>
    /// <param name="outputTotalFrames">The total number of frames delivered by the output's process.</param>
    [method: JsonConstructor]
    public class OutputStatusResponse(bool outputActive, bool outputReconnecting, string outputTimecode, long outputDuration, float outputCongestion, long outputBytes, long outputSkippedFrames, long outputTotalFrames) : IResponse
    {
        /// <summary>
        /// Gets a value indicating whether the output is active.
        /// </summary>
        [JsonPropertyName("outputActive")]
        public bool OutputActive { get; } = outputActive;

        /// <summary>
        /// Gets a value indicating whether the output is reconnecting.
        /// </summary>
        [JsonPropertyName("outputReconnecting")]
        public bool OutputReconnecting { get; } = outputReconnecting;

        /// <summary>
        /// Gets the formatted timecode string of the output.
        /// </summary>
        [JsonPropertyName("outputTimecode")]
        public string OutputTimecode { get; } = outputTimecode;

        /// <summary>
        /// Gets the duration of the output in milliseconds.
        /// </summary>
        [JsonPropertyName("outputDuration")]
        public long OutputDuration { get; } = outputDuration;

        /// <summary>
        /// Gets the congestion of the output.
        /// </summary>
        [JsonPropertyName("outputCongestion")]
        [JsonConverter(typeof(NullableNumberToNumberConverter))]
        public float OutputCongestion { get; } = outputCongestion;

        /// <summary>
        /// Gets the number of bytes sent by the output.
        /// </summary>
        [JsonPropertyName("outputBytes")]
        public long OutputBytes { get; } = outputBytes;

        /// <summary>
        /// Gets the number of frames skipped by the output's process.
        /// </summary>
        [JsonPropertyName("outputSkippedFrames")]
        public long OutputSkippedFrames { get; } = outputSkippedFrames;

        /// <summary>
        /// Gets the total number of frames delivered by the output's process.
        /// </summary>
        [JsonPropertyName("outputTotalFrames")]
        public long OutputTotalFrames { get; } = outputTotalFrames;
    }
}
