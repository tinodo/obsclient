namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetOutputSettings request.
    /// </summary>
    public class OutputSettingsResponse : IResponse
    {
        /// <summary>
        /// Gets the output settings.
        /// </summary>
        [JsonPropertyName("outputSettings")]
        public Dictionary<string, object> OutputSettings { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OutputSettingsResponse"/> class.
        /// </summary>
        /// <param name="outputSettings">The output settings.</param>
        [JsonConstructor]
        public OutputSettingsResponse(Dictionary<string, object> outputSettings)
        {
            this.OutputSettings = outputSettings;
        }
    }
}
