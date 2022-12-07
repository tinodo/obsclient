namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    public class InputAudioMonitorTypeResponseData : IResponseData
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("monitorType")]
        public MonitorType MonitorType { get; set; }

        public InputAudioMonitorTypeResponseData(MonitorType monitorType)
        {
            this.MonitorType = monitorType;
        }
    }
}
