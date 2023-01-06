namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the CurrentProfileChanging and CurrentProfileChanged events.
    /// </summary>
    public class ProfileNameEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the profile name.
        /// </summary>
        [JsonPropertyName("profileName")]
        public string ProfileName { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProfileNameEventArgs"/> class.
        /// </summary>
        /// <param name="profileName">The profile name.</param>
        [JsonConstructor]
        public ProfileNameEventArgs(string profileName)
        {
            this.ProfileName = profileName;
        }
    }
}
