namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetStats request.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="StatsResponse"/> class.
    /// </remarks>
    /// <param name="cpuUsage">The current CPU usage is percent.</param>
    /// <param name="memoryUsage">The amount of memory in MB currently being used by OBS Studio.</param>
    /// <param name="availableDiskSpace">The available disk space on the device being used for recording storage.</param>
    /// <param name="activeFps">The current frames per second being rendered.</param>
    /// <param name="averageFrameRenderTime">The average time, in milliseconds, that OBS Studio is taking to render a frame.</param>
    /// <param name="renderSkippedFrames">The number of frames skipped by OBS Studio in the render thread.</param>
    /// <param name="renderTotalFrames">The total number of frames outputted by the render thread.</param>
    /// <param name="outputSkippedFrames">The number of frames skipped by OBS Studio in the output thread.</param>
    /// <param name="outputTotalFrames">The total number of frames outputted by the output thread.</param>
    /// <param name="webSocketSessionIncomingMessages">The total number of messages received by obs-websocket from the client.</param>
    /// <param name="webSocketSessionOutgoingMessages">The total number of messages sent by osb-websocket to the client.</param>
    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetStats request.
    /// </summary>
    [method: JsonConstructor]
    public class StatsResponse(float cpuUsage, float memoryUsage, float availableDiskSpace, float activeFps, float averageFrameRenderTime, long renderSkippedFrames, long renderTotalFrames, long outputSkippedFrames, long outputTotalFrames, long webSocketSessionIncomingMessages, long webSocketSessionOutgoingMessages) : IResponse
    {
        /// <summary>
        /// Gets the current CPU usage is percent.
        /// </summary>
        [JsonPropertyName("cpuUsage")]
        public float CpuUsage { get; } = cpuUsage;

        /// <summary>
        /// Gets the amount of memory in MB currently being used by OBS Studio.
        /// </summary>
        [JsonPropertyName("memoryUsage")]
        public float MemoryUsage { get; } = memoryUsage;

        /// <summary>
        /// Gets the available disk space on the device being used for recording storage.
        /// </summary>
        [JsonPropertyName("availableDiskSpace")]
        public float AvailableDiskSpace { get; } = availableDiskSpace;

        /// <summary>
        /// Gets the current frames per second being rendered.
        /// </summary>
        [JsonPropertyName("activeFps")]
        public float ActiveFps { get; } = activeFps;

        /// <summary>
        /// Gets the average time, in milliseconds, that OBS Studio is taking to render a frame.
        /// </summary>
        [JsonPropertyName("averageFrameRenderTime")]
        public float AverageFrameRenderTime { get; } = averageFrameRenderTime;

        /// <summary>
        /// Gets the number of frames skipped by OBS Studio in the render thread.
        /// </summary>
        [JsonPropertyName("renderSkippedFrames")]
        public long RenderSkippedFrames { get; } = renderSkippedFrames;

        /// <summary>
        /// Gets the total number of frames outputted by the render thread.
        /// </summary>
        [JsonPropertyName("renderTotalFrames")]
        public long RenderTotalFrames { get; } = renderTotalFrames;

        /// <summary>
        /// Gets the number of frames skipped by OBS Studio in the output thread.
        /// </summary>
        [JsonPropertyName("outputSkippedFrames")]
        public long OutputSkippedFrames { get; } = outputSkippedFrames;

        /// <summary>
        /// Gets the total number of frames outputted by the output thread.
        /// </summary>
        [JsonPropertyName("outputTotalFrames")]
        public long OutputTotalFrames { get; } = outputTotalFrames;

        /// <summary>
        /// Gets the total number of messages received by obs-websocket from the client.
        /// </summary>
        [JsonPropertyName("webSocketSessionIncomingMessages")]
        public long WebSocketSessionIncomingMessages { get; } = webSocketSessionIncomingMessages;

        /// <summary>
        /// Gets the total number of messages sent by osb-websocket to the client.
        /// </summary>
        [JsonPropertyName("webSocketSessionOutgoingMessages")]
        public long WebSocketSessionOutgoingMessages { get; } = webSocketSessionOutgoingMessages;
    }
}
