namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class RecordStatusResponse : IResponse
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
        public RecordStatusResponse(bool outputActive, bool outputPaused, string outputTimecode, int outputDuration,int outputBytes)
        {
            this.OutputActive = outputActive;
            this.OutputPaused = outputPaused;
            this.OutputTimecode = outputTimecode;
            this.OutputDuration = outputDuration;
            this.OutputBytes = outputBytes;
        }
    }
}
