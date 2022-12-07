namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Classes;
    using OBSStudioClient.Interfaces;

    public class MonitorListResponse : IResponse
    {
        [JsonPropertyName("monitors")]
        public Monitor[] Monitors { get; set; }

        public MonitorListResponse(Monitor[] monitors)
        {
            this.Monitors = monitors ?? Array.Empty<Monitor>();
        }
    }
}
