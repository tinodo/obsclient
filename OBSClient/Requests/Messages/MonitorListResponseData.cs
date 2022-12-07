namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Classes;
    using OBSStudioClient.Interfaces;

    public class MonitorListResponseData : IResponseData
    {
        [JsonPropertyName("monitors")]
        public Monitor[] Monitors { get; set; }

        public MonitorListResponseData(Monitor[] monitors)
        {
            this.Monitors = monitors ?? Array.Empty<Monitor>();
        }
    }
}
