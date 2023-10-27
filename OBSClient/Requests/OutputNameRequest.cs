namespace OBSStudioClient.Requests
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Request Data for OBS Client Batch requests that require an outputName.
    /// </summary>
    public class OutputNameRequest
    {
        /// <summary>
        /// Gets or sets the output name.
        /// </summary>
        [JsonPropertyName("outputName")]
        public string OutputName { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OutputNameRequest"/> class.
        /// </summary>
        /// <param name="outputName">The output name.</param>
        [JsonConstructor]
        public OutputNameRequest(string outputName)
        {
            this.OutputName = outputName;
        }
    }
}
