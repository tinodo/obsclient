namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    public class ProfileNameEventArgs : EventArgs
    {
        [JsonPropertyName("profileName")]
        public string ProfileName { get; }

        [JsonConstructor]
        public ProfileNameEventArgs(string profileName)
        {
            this.ProfileName = profileName;
        }
    }
}
