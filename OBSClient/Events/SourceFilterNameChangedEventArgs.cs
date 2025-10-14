namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the SourceFilterEnableStateChanged event.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="SourceFilterNameChangedEventArgs"/> class.
    /// </remarks>
    /// <param name="sourceName">The source name.</param>
    /// <param name="oldFilterName">The old filter name.</param>
    /// <param name="filterName">The new filter name.</param>
    [method: JsonConstructor]
    public class SourceFilterNameChangedEventArgs(string sourceName, string oldFilterName, string filterName) : EventArgs
    {
        /// <summary>
        /// Gets the name of the source.
        /// </summary>
        [JsonPropertyName("sourceName")]
        public string SourceName { get; } = sourceName;

        /// <summary>
        /// Gets the old name of the filter.
        /// </summary>
        [JsonPropertyName("oldFilterName")]
        public string OldFilterName { get; } = oldFilterName;

        /// <summary>
        /// Gets the new name of the filter.
        /// </summary>
        [JsonPropertyName("filterName")]
        public string FilterName { get; } = filterName;
    }
}
