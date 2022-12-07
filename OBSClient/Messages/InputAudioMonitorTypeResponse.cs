namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    public class InputAudioMonitorTypeResponse : IResponse
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("monitorType")]
        public MonitorType MonitorType { get; set; }

        public InputAudioMonitorTypeResponse(MonitorType monitorType)
        {
            this.MonitorType = monitorType;
        }
    }
}
