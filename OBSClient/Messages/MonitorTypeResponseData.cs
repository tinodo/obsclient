namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    public class MonitorTypeResponseData : IResponseData
    {
        [JsonPropertyName("monitorType")]
        public MonitorType MonitorType { get; set; }

        public MonitorTypeResponseData(MonitorType monitorType)
        {
            this.MonitorType = monitorType;
        }
    }
}
