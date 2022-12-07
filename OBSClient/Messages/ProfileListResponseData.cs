namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class ProfileListResponseData : IResponseData
    {
        [JsonPropertyName("currentProfileName")]
        public string CurrentProfileName { get; set; }

        [JsonPropertyName("profiles")]
        public string[] Profiles { get; set; }

        [JsonConstructor]
        public ProfileListResponseData(string currentProfileName, string[] profiles)
        {
            this.CurrentProfileName = currentProfileName;
            this.Profiles = profiles ?? Array.Empty<string>();
        }
    }
}
