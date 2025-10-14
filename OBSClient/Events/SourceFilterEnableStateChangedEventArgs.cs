namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the SourceFilterEnableStateChanged event.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="SourceFilterEnableStateChangedEventArgs"/> class.
    /// </remarks>
    /// <param name="sourceName">The source name.</param>
    /// <param name="filterName">The filter name.</param>
    /// <param name="filterEnabled">A value indicating whether the filter is enabled.</param>
    [method: JsonConstructor]
    public class SourceFilterEnableStateChangedEventArgs(string sourceName, string filterName, bool filterEnabled) : EventArgs
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

        /// <summary>
        /// Gets a value indicating whether the filter is enabled.
        /// </summary>
        [JsonPropertyName("filterEnabled")]
        public bool FilterEnabled { get; } = filterEnabled;
    }
}
