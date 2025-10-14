namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetSourceFilterDefaultSettings request.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="DefaultFilterSettingsResponse"/> class.
    /// </remarks>
    /// <param name="defaultFilterSettings">Default Filter Settings.</param>
    [method: JsonConstructor]
    public class DefaultFilterSettingsResponse(Dictionary<string, object> defaultFilterSettings) : IResponse
    {
        /// <summary>
        /// Gets a dictionary with all the default filter settings.
        /// </summary>
        [JsonPropertyName("defaultFilterSettings")]
        public Dictionary<string, object> DefaultFilterSettings { get; } = defaultFilterSettings ?? [];
    }
}
