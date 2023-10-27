namespace OBSStudioClient.Requests
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Request Data for OBS Client Batch requests that require an outputName and outputSettings.
    /// </summary>
    public class OutputSettingsRequest : OutputNameRequest
    {
        /// <summary>
        /// Gets or sets the output settings.
        /// </summary>
        [JsonPropertyName("outputSettings")]
        public Dictionary<string, object> OutputSettings { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OutputSettingsRequest"/> class.
        /// </summary>
        /// <param name="outputName">The output name.</param>
        /// <param name="outputSettings">The output settings.</param>
        [JsonConstructor]
        public OutputSettingsRequest(string outputName, Dictionary<string, object> outputSettings) : base(outputName)
        {
            this.OutputSettings = outputSettings;
        }
    }
}
