namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetSourceFilterList request.
    /// </summary>
    public class FiltersResponse : IResponse
    {
        /// <summary>
        /// Gets the list of <see cref="Filter"/>.
        /// </summary>
        [JsonPropertyName("filters")]
        public Filter[] Filters { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FiltersResponse"/> class.
        /// </summary>
        /// <param name="filters">List of <see cref="Filter"/>.</param>
        public FiltersResponse(Filter[] filters)
        {
            this.Filters = filters ?? Array.Empty<Filter>();
        }
    }
}
