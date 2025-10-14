namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetSourceFilterKindList request.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="FilterKindListResponse"/> class.
    /// </remarks>
    /// <param name="sourceFilterKinds">List of <see cref="Filter"/>.</param>
    public class FilterKindListResponse(string[] sourceFilterKinds) : IResponse
    {
        /// <summary>
        /// Gets the list of source filter kinds.
        /// </summary>
        [JsonPropertyName("sourceFilterKinds")]
        public string[] SourceFilterKinds { get; } = sourceFilterKinds ?? [];
    }
}
