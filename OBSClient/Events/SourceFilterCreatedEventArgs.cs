namespace OBSStudioClient.Events
{
    using OBSStudioClient.Classes;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the ScreenshotSaved event.
    /// </summary>
    public class SourceFilterCreatedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the name of the source.
        /// </summary>
        [JsonPropertyName("sourceName")]
        public string SourceName { get; }

        /// <summary>
        /// Gets the name of the filter.
        /// </summary>
        [JsonPropertyName("filterName")]
        public string FilterName { get; }

        /// <summary>
        /// Gets the filter kind.
        /// </summary>
        [JsonPropertyName("filterKind")]
        public string FilterKind { get; }

        /// <summary>
        /// Gets the filter index.
        /// </summary>
        [JsonPropertyName("filterIndex")]
        public int FilterIndex { get; }

        /// <summary>
        /// Gets the <see cref="FilterSettings"/>
        /// </summary>
        [JsonPropertyName("filterSettings")]
        public FilterSettings FilterSettings { get; }

        /// <summary>
        /// Gets the default filter settings.
        /// </summary>
        [JsonPropertyName("defaultFilterSettings")]
        public Dictionary<string, object> DefaultFilterSettings { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SourceFilterCreatedEventArgs"/> class.
        /// </summary>
        /// <param name="sourceName">The source name.</param>
        /// <param name="filterName">The filter name.</param>
        /// <param name="filterKind">The filter kind.</param>
        /// <param name="filterIndex">The filter index.</param>
        /// <param name="filterSettings">The filter settings.</param>
        /// <param name="defaultFilterSettings">The default filter settings.</param>
        [JsonConstructor]
        public SourceFilterCreatedEventArgs(string sourceName, string filterName, string filterKind, int filterIndex, FilterSettings filterSettings, Dictionary<string, object> defaultFilterSettings)
        {
            this.SourceName = sourceName;
            this.FilterName = filterName;
            this.FilterKind = filterKind;
            this.FilterIndex = filterIndex;
            this.FilterSettings = filterSettings;
            this.DefaultFilterSettings = defaultFilterSettings;
        }
    }
}
