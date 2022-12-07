namespace OBSStudioClient.Events
{
    using OBSStudioClient.Enums;
    using System.Text.Json.Serialization;

    public class RecordStateChangedEventArgs : EventArgs
    {
        [JsonPropertyName("outputActive")]
        public bool OutputActive { get; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("outputState")]
        public ObsOutputState OutputState { get; }

        [JsonPropertyName("outputPath")]
        public string OutputPath { get; }

        [JsonConstructor]
        public RecordStateChangedEventArgs(bool outputActive, ObsOutputState outputState, string outputPath)
        {
            this.OutputActive = outputActive;
            this.OutputState = outputState;
            this.OutputPath = outputPath;
        }
    }
}
