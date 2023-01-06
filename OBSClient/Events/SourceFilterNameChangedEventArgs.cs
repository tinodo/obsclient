namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the SourceFilterEnableStateChanged event.
    /// </summary>
    public class SourceFilterNameChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the name of the source.
        /// </summary>
        [JsonPropertyName("sourceName")]
        public string SourceName { get; }

        /// <summary>
        /// Gets the old name of the filter.
        /// </summary>
        [JsonPropertyName("oldFilterName")]
        public string OldFilterName { get; }

        /// <summary>
        /// Gets the new name of the filter.
        /// </summary>
        [JsonPropertyName("filterName")]
        public string FilterName { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SourceFilterNameChangedEventArgs"/> class.
        /// </summary>
        /// <param name="sourceName">The source name.</param>
        /// <param name="oldFilterName">The old filter name.</param>
        /// <param name="filterName">The new filter name.</param>
        [JsonConstructor]
        public SourceFilterNameChangedEventArgs(string sourceName, string oldFilterName, string filterName)
        {
            this.SourceName = sourceName;
            this.OldFilterName = oldFilterName;
            this.FilterName = filterName;
        }
    }
}
