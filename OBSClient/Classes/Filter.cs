namespace OBSStudioClient.Classes
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides a class for Filter.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="Filter"/> class.
    /// </remarks>
    /// <param name="filterEnabled">A value indicating whether the filter is enabled.</param>
    /// <param name="filterIndex">The filter index.</param>
    /// <param name="filterKind">The filter kind.</param>
    /// <param name="filterName">The filter name.</param>
    /// <param name="filterSettings">The <see cref="FilterSettings"/>.</param>
    [method: JsonConstructor]
    public class Filter(bool filterEnabled, int filterIndex, string filterKind, string filterName, Dictionary<string, object> filterSettings)
    {
        /// <summary>
        /// Gets a value indicating whether the filter is enabled.
        /// </summary>
        [JsonPropertyName("filterEnabled")]
        public bool FilterEnabled { get; } = filterEnabled;

        /// <summary>
        /// Gets the filter index.
        /// </summary>
        [JsonPropertyName("filterIndex")]
        public int FilterIndex { get; } = filterIndex;

        /// <summary>
        /// Gets the filter kind.
        /// </summary>
        [JsonPropertyName("filterKind")]
        public string FilterKind { get; } = filterKind;

        /// <summary>
        /// Gets the filter name.
        /// </summary>
        [JsonPropertyName("filterName")]
        public string FilterName { get; } = filterName;

        /// <summary>
        /// Gets the <see cref="FilterSettings"/>.
        /// </summary>
        [JsonPropertyName("filterSettings")]
        public Dictionary<string, object> FilterSettings { get; } = filterSettings ?? [];
    }
}
