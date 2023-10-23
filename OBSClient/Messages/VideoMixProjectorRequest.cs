namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Enums;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Request Data for OBS Client Batch requests that require a videoMixType, monitorIndex and projectorGeometry.
    /// </summary>
    public class VideoMixProjectorRequest
    {
        /// <summary>
        /// Gets or sets the <see cref="MixType"/>.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("videoMixType")]
        public MixType VideoMixType { get; set; }

        /// <summary>
        /// Gets or sets the monitor index.
        /// </summary>
        [JsonPropertyName("monitorIndex")]
        public int MonitorIndex { get; set; }

        /// <summary>
        /// Gets or sets the projector geometry.
        /// </summary>
        /// <remarks>
        /// See <see cref="ObsClient.GetGeometry"/>.
        /// </remarks>
        [JsonPropertyName("projectorGeometry")]
        public string ProjectorGeometry { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="VideoMixProjectorRequest"/> class.
        /// </summary>
        /// <param name="videoMixType">The <see cref="MixType"/>.</param>
        /// <param name="monitorIndex">The monitor index.</param>
        /// <param name="projectorGeometry">The projector geometry.</param>
        [JsonConstructor]
        public VideoMixProjectorRequest(MixType videoMixType, int monitorIndex, string projectorGeometry)
        {
            this.VideoMixType = videoMixType;
            this.MonitorIndex = monitorIndex;
            this.ProjectorGeometry = projectorGeometry;
        }
    }
}
