namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetSourceFilterList request.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="FiltersResponse"/> class.
    /// </remarks>
    /// <param name="filters">List of <see cref="Filter"/>.</param>
    public class FiltersResponse(Filter[] filters) : IResponse
    {
        /// <summary>
        /// Gets the list of <see cref="Filter"/>.
        /// </summary>
        [JsonPropertyName("filters")]
        public Filter[] Filters { get; } = filters ?? [];
    }
}
