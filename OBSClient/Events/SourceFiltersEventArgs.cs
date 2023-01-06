namespace OBSStudioClient.Events
{
    using OBSStudioClient.Classes;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the SourceFilterListReindexed event.
    /// </summary>
    public class SourceFiltersEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the name of the source.
        /// </summary>
        [JsonPropertyName("sourceName")]
        public string SourceName { get; }

        /// <summary>
        /// Gets the list of <see cref="Filter"/>.
        /// </summary>
        [JsonPropertyName("filters")]
        public Filter[] Filters { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SourceFiltersEventArgs"/> class.
        /// </summary>
        /// <param name="sourceName">The source name.</param>
        /// <param name="filters">The list of <see cref="Filter"/>.</param>
        [JsonConstructor]
        public SourceFiltersEventArgs(string sourceName, Filter[] filters)
        {
            this.SourceName = sourceName;
            this.Filters = filters ?? Array.Empty<Filter>();
        }
    }
}
