namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class GroupsResponseData : IResponseData
    {
        [JsonPropertyName("groups")]
        public string[] Groups { get;set; }

        [JsonConstructor]
        public GroupsResponseData(string[] groups)
        {
            this.Groups = groups ?? Array.Empty<string>();
        }
    }
}
