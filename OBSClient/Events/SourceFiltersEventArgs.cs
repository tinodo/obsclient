namespace OBSStudioClient.Events
{
    using OBSStudioClient.Classes;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the SourceFilterListReindexed event.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="SourceFiltersEventArgs"/> class.
    /// </remarks>
    /// <param name="sourceName">The source name.</param>
    /// <param name="filters">The list of <see cref="Filter"/>.</param>
    [method: JsonConstructor]
    public class SourceFiltersEventArgs(string sourceName, Filter[] filters) : EventArgs
    {
        /// <summary>
        /// Gets the name of the source.
        /// </summary>
        [JsonPropertyName("sourceName")]
        public string SourceName { get; } = sourceName;

        /// <summary>
        /// Gets the list of <see cref="Filter"/>.
        /// </summary>
        [JsonPropertyName("filters")]
        public Filter[] Filters { get; } = filters ?? [];
    }
}
