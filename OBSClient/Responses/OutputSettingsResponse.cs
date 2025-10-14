namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetOutputSettings request.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="OutputSettingsResponse"/> class.
    /// </remarks>
    /// <param name="outputSettings">The output settings.</param>
    [method: JsonConstructor]
    public class OutputSettingsResponse(Dictionary<string, object> outputSettings) : IResponse
    {
        /// <summary>
        /// Gets the output settings.
        /// </summary>
        [JsonPropertyName("outputSettings")]
        public Dictionary<string, object> OutputSettings { get; } = outputSettings;
    }
}
