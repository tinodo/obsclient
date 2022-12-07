namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class ProfileListResponse : IResponse
    {
        [JsonPropertyName("currentProfileName")]
        public string CurrentProfileName { get; set; }

        [JsonPropertyName("profiles")]
        public string[] Profiles { get; set; }

        [JsonConstructor]
        public ProfileListResponse(string currentProfileName, string[] profiles)
        {
            this.CurrentProfileName = currentProfileName;
            this.Profiles = profiles ?? Array.Empty<string>();
        }
    }
}
