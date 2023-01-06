namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetGroupList request.
    /// </summary>
    public class GroupsResponse : IResponse
    {
        /// <summary>
        /// Gets the list of groupnames.
        /// </summary>
        [JsonPropertyName("groups")]
        public string[] Groups { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GroupsResponse"/> class.
        /// </summary>
        /// <param name="groups">List of groupnames.</param>
        [JsonConstructor]
        public GroupsResponse(string[] groups)
        {
            this.Groups = groups ?? Array.Empty<string>();
        }
    }
}
