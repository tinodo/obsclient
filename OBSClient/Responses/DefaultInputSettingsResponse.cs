namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetInputDefaultSettings request.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="DefaultInputSettingsResponse"/> class.
    /// </remarks>
    /// <param name="defaultInputSettings">Default Input Settings.</param>
    [method: JsonConstructor]
    public class DefaultInputSettingsResponse(Dictionary<string, object> defaultInputSettings) : IResponse
    {
        /// <summary>
        /// Gets a dictionary with all the default input settings.
        /// </summary>
        [JsonPropertyName("defaultInputSettings")]
        public Dictionary<string, object> DefaultInputSettings { get; } = defaultInputSettings ?? [];
    }
}
