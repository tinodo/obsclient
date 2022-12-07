namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class OutputStatusResponse : IResponse
    {
        [JsonPropertyName("outputActive")]
        public bool OutputActive { get; set; }

        [JsonPropertyName("outputReconnecting")]
        public bool OutputReconnecting { get; set; }


        [JsonPropertyName("outputTimecode")]
        public string OutputTimecode { get; set; }


        [JsonPropertyName("outputDuration")]
        public long OutputDuration { get; set; }


        [JsonPropertyName("outputCongestion")]
        public float OutputCongestion { get; set; }


        [JsonPropertyName("outputBytes")]
        public long OutputBytes { get; set; }


        [JsonPropertyName("outputSkippedFrames")]
        public long OutputSkippedFrames { get; set; }


        [JsonPropertyName("outputTotalFrames")]
        public long OutputTotalFrames { get; set; }

        [JsonConstructor]
        public OutputStatusResponse(bool outputActive, bool outputReconnecting, string outputTimecode, long outputDuration, float outputCongestion, long outputBytes, long outputSkippedFrames, long outputTotalFrames)
        {
            this.OutputActive = outputActive;
            this.OutputReconnecting = outputReconnecting;
            this.OutputTimecode = outputTimecode;
            this.OutputDuration = outputDuration;
            this.OutputCongestion = outputCongestion;
            this.OutputBytes = outputBytes;
            this.OutputSkippedFrames = outputSkippedFrames;
            this.OutputTotalFrames = outputTotalFrames;
        }
    }
}
