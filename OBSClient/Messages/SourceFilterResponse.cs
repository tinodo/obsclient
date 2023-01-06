namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetSourceFilter request.
    /// </summary>
    public class SourceFilterResponse : IResponse
    {
        /// <summary>
        /// Gets a value indicating whether the filter is enabled.
        /// </summary>
        [JsonPropertyName("filterEnabled")]
        public bool FilterEnabled { get; }

        /// <summary>
        /// Gets the index of the filter.
        /// </summary>
        [JsonPropertyName("filterIndex")]
        public int FilterIndex { get; }

        /// <summary>
        /// Gets the filter kind.
        /// </summary>
        [JsonPropertyName("filterKind")]
        public string FilterKind { get; }

        /// <summary>
        /// Gets the <see cref="FilterSettings"/>.
        /// </summary>
        [JsonPropertyName("filterSettings")]
        public FilterSettings FilterSettings { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SourceFilterResponse"/> class.
        /// </summary>
        /// <param name="filterEnabled">The value indicating whether the filter is enabled.</param>
        /// <param name="filterIndex">The index of the filter.</param>
        /// <param name="filterKind">The filter kind.</param>
        /// <param name="filterSettings">The <see cref="FilterSettings"/>.</param>
        [JsonConstructor]
        public SourceFilterResponse(bool filterEnabled, int filterIndex, string filterKind, FilterSettings filterSettings)
        {
            this.FilterEnabled = filterEnabled;
            this.FilterIndex = filterIndex;
            this.FilterKind = filterKind;
            this.FilterSettings = filterSettings;
        }
    }
}
