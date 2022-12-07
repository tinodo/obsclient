namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class StatsResponse : IResponse
    {
        [JsonPropertyName("cpuUsage")]
        public float CpuUsage { get; set; }

        [JsonPropertyName("memoryUsage")]
        public float MemoryUsage { get; set; }

        [JsonPropertyName("availableDiskSpace")]
        public float AvailableDiskSpace { get; set; }

        [JsonPropertyName("activeFps")]
        public float ActiveFps { get; set; }

        [JsonPropertyName("averageFrameRenderTime")]
        public float AverageFrameRenderTime { get; set; }

        [JsonPropertyName("renderSkippedFrames")]
        public long RenderSkippedFrames { get; set; }

        [JsonPropertyName("renderTotalFrames")]
        public long RenderTotalFrames { get; set; }

        [JsonPropertyName("outputSkippedFrames")]
        public long OutputSkippedFrames { get; set; }

        [JsonPropertyName("outputTotalFrames")]
        public long OutputTotalFrames { get; set; }

        [JsonPropertyName("webSocketSessionIncomingMessages")]
        public long WebSocketSessionIncomingMessages { get; set; }

        [JsonPropertyName("webSocketSessionOutgoingMessages")]
        public long WebSocketSessionOutgoingMessages { get; set; }

        [JsonConstructor]
        public StatsResponse(float cpuUsage, float memoryUsage, float availableDiskSpace, float activeFps, float averageFrameRenderTime, long renderSkippedFrames, long renderTotalFrames, long outputSkippedFrames, long outputTotalFrames, long webSocketSessionIncomingMessages, long webSocketSessionOutgoingMessages)
        {
            this.CpuUsage = cpuUsage;
            this.MemoryUsage = memoryUsage;
            this.AvailableDiskSpace = availableDiskSpace;
            this.ActiveFps = activeFps;
            this.AverageFrameRenderTime = averageFrameRenderTime;
            this.RenderSkippedFrames = renderSkippedFrames;
            this.RenderTotalFrames = renderTotalFrames;
            this.OutputSkippedFrames = outputSkippedFrames;
            this.OutputTotalFrames = outputTotalFrames;
            this.WebSocketSessionIncomingMessages = webSocketSessionIncomingMessages;
            this.WebSocketSessionOutgoingMessages = webSocketSessionOutgoingMessages;
        }
    }
}
