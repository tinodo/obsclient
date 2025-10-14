namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the SourceFilterSettingsChanged event.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="SourceFilterSettingsChangedEventArgs"/> class.
    /// </remarks>
    /// <param name="sourceName">The source name.</param>
    /// <param name="filterName">The filter name.</param>
    /// <param name="filterSettings">The filter settings.</param>
    [method: JsonConstructor]
    public class SourceFilterSettingsChangedEventArgs(string sourceName, string filterName, Dictionary<string, object> filterSettings) : EventArgs
    {
        /// <summary>
        /// Gets the name of the source.
        /// </summary>
        [JsonPropertyName("sourceName")]
        public string SourceName { get; } = sourceName;

        /// <summary>
        /// Gets the name of the filter.
        /// </summary>
        [JsonPropertyName("filterName")]
        public string FilterName { get; } = filterName;

        /// <summary>
        /// Gets the <see cref="FilterSettings"/>
        /// </summary>
        [JsonPropertyName("filterSettings")]
        public Dictionary<string, object> FilterSettings { get; } = filterSettings;
    }
}
