namespace OBSStudioClient.Events
{
    using OBSStudioClient.Enums;
    using System.Text.Json.Serialization;

    public class OutputStateChangedEventArgs : EventArgs
    {
        [JsonPropertyName("outputActive")]
        public bool OutputActive { get; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("outputState")]
        public ObsOutputState OutputState { get; }

        [JsonConstructor]
        public OutputStateChangedEventArgs(bool outputActive, ObsOutputState outputState)
        {
            this.OutputActive = outputActive;
            this.OutputState = outputState;
        }
    }
}
