namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class StatsResponseData : IResponseData
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
        public int RenderSkippedFrames { get; set; }

        [JsonPropertyName("renderTotalFrames")]
        public int RenderTotalFrames { get; set; }

        [JsonPropertyName("outputSkippedFrames")]
        public int OutputSkippedFrames { get; set; }

        [JsonPropertyName("outputTotalFrames")]
        public int OutputTotalFrames { get; set; }

        [JsonPropertyName("webSocketSessionIncomingMessages")]
        public int WebSocketSessionIncomingMessages { get; set; }

        [JsonPropertyName("webSocketSessionOutgoingMessages")]
        public int WebSocketSessionOutgoingMessages { get; set; }

        [JsonConstructor]
        public StatsResponseData(float cpuUsage, float memoryUsage, float availableDiskSpace, float activeFps, float averageFrameRenderTime, int renderSkippedFrames, int renderTotalFrames, int outputSkippedFrames, int outputTotalFrames, int webSocketSessionIncomingMessages, int webSocketSessionOutgoingMessages)
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
