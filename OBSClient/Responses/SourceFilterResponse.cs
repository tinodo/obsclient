namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetSourceFilter request.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="SourceFilterResponse"/> class.
    /// </remarks>
    /// <param name="filterEnabled">The value indicating whether the filter is enabled.</param>
    /// <param name="filterIndex">The index of the filter.</param>
    /// <param name="filterKind">The filter kind.</param>
    /// <param name="filterSettings">The <see cref="FilterSettings"/>.</param>
    [method: JsonConstructor]
    public class SourceFilterResponse(bool filterEnabled, int filterIndex, string filterKind, Dictionary<string, object> filterSettings) : IResponse
    {
        /// <summary>
        /// Gets a value indicating whether the filter is enabled.
        /// </summary>
        [JsonPropertyName("filterEnabled")]
        public bool FilterEnabled { get; } = filterEnabled;

        /// <summary>
        /// Gets the index of the filter.
        /// </summary>
        [JsonPropertyName("filterIndex")]
        public int FilterIndex { get; } = filterIndex;

        /// <summary>
        /// Gets the filter kind.
        /// </summary>
        [JsonPropertyName("filterKind")]
        public string FilterKind { get; } = filterKind;

        /// <summary>
        /// Gets the <see cref="FilterSettings"/>.
        /// </summary>
        [JsonPropertyName("filterSettings")]
        public Dictionary<string, object> FilterSettings { get; } = filterSettings ?? [];
    }
}
