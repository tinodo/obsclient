namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Request Data for OBS Client Batch requests that require a sourceName.
    /// </summary>
    public class SourceNameRequest
    {
        /// <summary>
        /// Gets or sets the source name.
        /// </summary>
        [JsonPropertyName("sourceName")]
        public string SourceName { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SourceNameRequest"/> class.
        /// </summary>
        /// <param name="sourceName">The source name.</param>
        [JsonConstructor]
        public SourceNameRequest(string sourceName)
        {
            this.SourceName = sourceName;
        }
    }
}
