namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetProfileList request.
    /// </summary>
    public class ProfileListResponse : IResponse
    {
        /// <summary>
        /// Gets the name of the current profile.
        /// </summary>
        [JsonPropertyName("currentProfileName")]
        public string CurrentProfileName { get; }

        /// <summary>
        /// Gets the list of all profile names.
        /// </summary>
        [JsonPropertyName("profiles")]
        public string[] Profiles { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProfileListResponse"/> class.
        /// </summary>
        /// <param name="currentProfileName">the name of the current profile.</param>
        /// <param name="profiles">The list of all profiel names.</param>
        [JsonConstructor]
        public ProfileListResponse(string currentProfileName, string[] profiles)
        {
            this.CurrentProfileName = currentProfileName;
            this.Profiles = profiles ?? Array.Empty<string>();
        }
    }
}
