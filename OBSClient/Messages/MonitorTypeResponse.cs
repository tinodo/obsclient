namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    public class MonitorTypeResponse : IResponse
    {
        [JsonPropertyName("monitorType")]
        public MonitorType MonitorType { get; set; }

        public MonitorTypeResponse(MonitorType monitorType)
        {
            this.MonitorType = monitorType;
        }
    }
}
