namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the ProfileListChanged event.
    /// </summary>
    public class ProfileListEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the list of profiles.
        /// </summary>
        [JsonPropertyName("profiles")]
        public string[] Profiles { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProfileListEventArgs"/> class.
        /// </summary>
        /// <param name="profiles">The list of profiles.</param>
        [JsonConstructor]
        public ProfileListEventArgs(string[] profiles)
        {
            this.Profiles = profiles ?? Array.Empty<string>();
        }
    }
}
