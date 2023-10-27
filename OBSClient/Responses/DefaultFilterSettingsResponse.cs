namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetSourceFilterDefaultSettings request.
    /// </summary>
    public class DefaultFilterSettingsResponse : IResponse
    {
        /// <summary>
        /// Gets a dictionary with all the default filter settings.
        /// </summary>
        [JsonPropertyName("defaultFilterSettings")]
        public Dictionary<string, object> DefaultFilterSettings { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultFilterSettingsResponse"/> class.
        /// </summary>
        /// <param name="defaultFilterSettings">Default Filter Settings.</param>
        [JsonConstructor]
        public DefaultFilterSettingsResponse(Dictionary<string, object> defaultFilterSettings)
        {
            this.DefaultFilterSettings = defaultFilterSettings ?? new();
        }
    }
}
