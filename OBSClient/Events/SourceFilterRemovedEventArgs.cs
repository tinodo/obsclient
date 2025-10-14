namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the SourceFilterRemoved event.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="SourceFilterRemovedEventArgs"/> class.
    /// </remarks>
    /// <param name="sourceName">The source name.</param>
    /// <param name="filterName">The filter name.</param>
    [method: JsonConstructor]
    public class SourceFilterRemovedEventArgs(string sourceName, string filterName) : EventArgs
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
    }
}
