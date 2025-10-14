namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetGroupList request.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="GroupsResponse"/> class.
    /// </remarks>
    /// <param name="groups">List of groupnames.</param>
    [method: JsonConstructor]
    public class GroupsResponse(string[] groups) : IResponse
    {
        /// <summary>
        /// Gets the list of groupnames.
        /// </summary>
        [JsonPropertyName("groups")]
        public string[] Groups { get; } = groups ?? [];
    }
}
