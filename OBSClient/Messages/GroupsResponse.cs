namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class GroupsResponse : IResponse
    {
        [JsonPropertyName("groups")]
        public string[] Groups { get;set; }

        [JsonConstructor]
        public GroupsResponse(string[] groups)
        {
            this.Groups = groups ?? Array.Empty<string>();
        }
    }
}
