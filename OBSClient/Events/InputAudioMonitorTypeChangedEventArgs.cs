namespace OBSStudioClient.Events
{
    using OBSStudioClient.Enums;
    using System.Text.Json.Serialization;

    public class InputAudioMonitorTypeChangedEventArgs : EventArgs
    {
        [JsonPropertyName("inputName")]
        public string InputName { get; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("monitorType")]
        public MonitorType MonitorType { get; }

        [JsonConstructor]
        public InputAudioMonitorTypeChangedEventArgs(string inputName, MonitorType monitorType)
        {
            this.InputName = inputName;
            this.MonitorType = monitorType;
        }
    }
}
