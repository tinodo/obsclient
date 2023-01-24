namespace OBSStudioClient.Classes
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides a class for Filter.
    /// </summary>
    public class Filter
    {
        /// <summary>
        /// Gets a value indicating whether the filter is enabled.
        /// </summary>
        [JsonPropertyName("filterEnabled")]
        public bool FilterEnabled { get; }

        /// <summary>
        /// Gets the filter index.
        /// </summary>
        [JsonPropertyName("filterIndex")]
        public int FilterIndex { get; }

        /// <summary>
        /// Gets the filter kind.
        /// </summary>
        [JsonPropertyName("filterKind")]
        public string FilterKind { get; }

        /// <summary>
        /// Gets the filter name.
        /// </summary>
        [JsonPropertyName("filterName")]
        public string FilterName { get; }

        /// <summary>
        /// Gets the <see cref="FilterSettings"/>.
        /// </summary>
        [JsonPropertyName("filterSettings")]
        //public FilterSettings FilterSettings { get; }
        public Dictionary<string, object> FilterSettings { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Filter"/> class.
        /// </summary>
        /// <param name="filterEnabled">A value indicating whether the filter is enabled.</param>
        /// <param name="filterIndex">The filter index.</param>
        /// <param name="filterKind">The filter kind.</param>
        /// <param name="filterName">The filter name.</param>
        /// <param name="filterSettings">The <see cref="FilterSettings"/>.</param>
        [JsonConstructor]
        public Filter(bool filterEnabled, int filterIndex, string filterKind, string filterName, Dictionary<string, object> filterSettings)
        {
            this.FilterEnabled = filterEnabled;
            this.FilterIndex = filterIndex;
            this.FilterKind = filterKind;
            this.FilterName = filterName;
            this.FilterSettings = filterSettings ?? new();
        }
    }
}
