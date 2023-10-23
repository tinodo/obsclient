namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Request Data for OBS Client Batch requests that require a recordDirectory.
    /// </summary>
    public class RecordDirectoryRequest
    {
        /// <summary>
        /// Gets or sets the record directory.
        /// </summary>
        [JsonPropertyName("recordDirectory")]
        public string RecordDirectory { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RecordDirectoryRequest"/> class.
        /// </summary>
        /// <param name="recordDirectory">The record directory.</param>
        [JsonConstructor]
        public RecordDirectoryRequest(string recordDirectory)
        {
            this.RecordDirectory = recordDirectory;
        }
    }
}
