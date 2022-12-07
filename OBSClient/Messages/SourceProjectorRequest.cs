namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;

    public class SourceProjectorRequest
    {
        [JsonPropertyName("sourceName")]
        public string SourceName { get; set; }

        [JsonPropertyName("monitorIndex")]
        public int MonitorIndex { get; set; }

        [JsonPropertyName("projectorGeometry")]
        public string ProjectorGeometry { get; set; }

        [JsonConstructor]
        public SourceProjectorRequest(string sourceName, int monitorIndex, string projectorGeometry)
        {
            this.SourceName = sourceName;
            this.MonitorIndex = monitorIndex;
            this.ProjectorGeometry = projectorGeometry;
        }
    }
}
