namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the ScreenshotSaved event.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="SourceFilterCreatedEventArgs"/> class.
    /// </remarks>
    /// <param name="sourceName">The source name.</param>
    /// <param name="filterName">The filter name.</param>
    /// <param name="filterKind">The filter kind.</param>
    /// <param name="filterIndex">The filter index.</param>
    /// <param name="filterSettings">The filter settings.</param>
    /// <param name="defaultFilterSettings">The default filter settings.</param>
    [method: JsonConstructor]
    public class SourceFilterCreatedEventArgs(string sourceName, string filterName, string filterKind, int filterIndex, Dictionary<string, object> filterSettings, Dictionary<string, object> defaultFilterSettings) : EventArgs
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
        /// Gets the filter kind.
        /// </summary>
        [JsonPropertyName("filterKind")]
        public string FilterKind { get; } = filterKind;

        /// <summary>
        /// Gets the filter index.
        /// </summary>
        [JsonPropertyName("filterIndex")]
        public int FilterIndex { get; } = filterIndex;

        /// <summary>
        /// Gets the <see cref="FilterSettings"/>
        /// </summary>
        [JsonPropertyName("filterSettings")]
        public Dictionary<string, object> FilterSettings { get; } = filterSettings;

        /// <summary>
        /// Gets the default filter settings.
        /// </summary>
        [JsonPropertyName("defaultFilterSettings")]
        public Dictionary<string, object> DefaultFilterSettings { get; } = defaultFilterSettings;
    }
}
