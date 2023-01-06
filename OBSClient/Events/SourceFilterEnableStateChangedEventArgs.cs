namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the SourceFilterEnableStateChanged event.
    /// </summary>
    public class SourceFilterEnableStateChangedEventArgs : EventArgs
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
        /// Gets a value indicating whether the filter is enabled.
        /// </summary>
        [JsonPropertyName("filterEnabled")]
        public bool FilterEnabled { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SourceFilterEnableStateChangedEventArgs"/> class.
        /// </summary>
        /// <param name="sourceName">The source name.</param>
        /// <param name="filterName">The filter name.</param>
        /// <param name="filterEnabled">A value indicating whether the filter is enabled.</param>
        [JsonConstructor]
        public SourceFilterEnableStateChangedEventArgs(string sourceName, string filterName, bool filterEnabled)
        {
            this.SourceName = sourceName;
            this.FilterName = filterName;
            this.FilterEnabled = filterEnabled;
        }
    }
}
