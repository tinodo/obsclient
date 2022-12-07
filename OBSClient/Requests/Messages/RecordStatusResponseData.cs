namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class RecordStatusResponseData : IResponseData
    {
        [JsonPropertyName("outputActive")]
        public bool OutputActive { get; set; }

        [JsonPropertyName("outputPaused")]
        public bool OutputPaused { get; set; }


        [JsonPropertyName("outputTimecode")]
        public string OutputTimecode { get; set; }


        [JsonPropertyName("outputDuration")]
        public int OutputDuration { get; set; }


        [JsonPropertyName("outputBytes")]
        public int OutputBytes { get; set; }


        [JsonConstructor]
        public RecordStatusResponseData(bool outputActive, bool outputPaused, string outputTimecode, int outputDuration,int outputBytes)
        {
            this.OutputActive = outputActive;
            this.OutputPaused = outputPaused;
            this.OutputTimecode = outputTimecode;
            this.OutputDuration = outputDuration;
            this.OutputBytes = outputBytes;
        }
    }
}
