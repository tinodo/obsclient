namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Enums;
    using System.Text.Json.Serialization;

    public class VideoMixProjectorRequestData
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("videoMixType")]
        public MixType VideoMixType { get; set; }

        [JsonPropertyName("monitorIndex")]
        public int MonitorIndex { get; set; }

        [JsonPropertyName("projectorGeometry")]
        public string ProjectorGeometry { get; set; }

        [JsonConstructor]
        public VideoMixProjectorRequestData(MixType videoMixType, int monitorIndex, string projectorGeometry)
        {
            this.VideoMixType = videoMixType;
            this.MonitorIndex = monitorIndex;
            this.ProjectorGeometry = projectorGeometry;
        }
    }
}
