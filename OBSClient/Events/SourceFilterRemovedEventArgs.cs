namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the SourceFilterRemoved event.
    /// </summary>
    public class SourceFilterRemovedEventArgs : EventArgs
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
        /// Initializes a new instance of the <see cref="SourceFilterRemovedEventArgs"/> class.
        /// </summary>
        /// <param name="sourceName">The source name.</param>
        /// <param name="filterName">The filter name.</param>
        [JsonConstructor]
        public SourceFilterRemovedEventArgs(string sourceName, string filterName)
        {
            this.SourceName = sourceName;
            this.FilterName = filterName;
        }
    }
}
