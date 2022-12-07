namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class OutputStatusResponseData : IResponseData
    {
        [JsonPropertyName("outputActive")]
        public bool OutputActive { get; set; }

        [JsonPropertyName("outputReconnecting")]
        public bool OutputReconnecting { get; set; }


        [JsonPropertyName("outputTimecode")]
        public string OutputTimecode { get; set; }


        [JsonPropertyName("outputDuration")]
        public int OutputDuration { get; set; }


        [JsonPropertyName("outputCongestion")]
        public float OutputCongestion { get; set; }


        [JsonPropertyName("outputBytes")]
        public int OutputBytes { get; set; }


        [JsonPropertyName("outputSkippedFrames")]
        public int OutputSkippedFrames { get; set; }


        [JsonPropertyName("outputTotalFrames")]
        public int OutputTotalFrames { get; set; }

        [JsonConstructor]
        public OutputStatusResponseData(bool outputActive, bool outputReconnecting, string outputTimecode, int outputDuration, float outputCongestion, int outputBytes, int outputSkippedFrames, int outputTotalFrames)
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
