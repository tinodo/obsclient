namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    public class ProfileListEventArgs : EventArgs
    {
        [JsonPropertyName("profiles")]
        public string[] Profiles { get; }

        [JsonConstructor]
        public ProfileListEventArgs(string[] profiles)
        {
            this.Profiles = profiles ?? Array.Empty<string>();
        }
    }
}
