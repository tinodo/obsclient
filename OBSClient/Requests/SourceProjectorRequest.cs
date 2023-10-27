namespace OBSStudioClient.Requests
{
    using OBSStudioClient;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Request Data for OBS Client Batch requests that require a sourceName, a monitorIndex and a projectorGeometry.
    /// </summary>
    public class SourceProjectorRequest
    {
        /// <summary>
        /// Gets or sets the source name.
        /// </summary>
        [JsonPropertyName("sourceName")]
        public string SourceName { get; set; }

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
        /// Initializes a new instance of the <see cref="SourceProjectorRequest"/> class.
        /// </summary>
        /// <param name="sourceName">The source name.</param>
        /// <param name="monitorIndex">The monitor index.</param>
        /// <param name="projectorGeometry">The projector geometry.</param>
        [JsonConstructor]
        public SourceProjectorRequest(string sourceName, int monitorIndex, string projectorGeometry)
        {
            this.SourceName = sourceName;
            this.MonitorIndex = monitorIndex;
            this.ProjectorGeometry = projectorGeometry;
        }
    }
}
