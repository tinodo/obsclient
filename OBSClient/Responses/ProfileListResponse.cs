namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetProfileList request.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="ProfileListResponse"/> class.
    /// </remarks>
    /// <param name="currentProfileName">the name of the current profile.</param>
    /// <param name="profiles">The list of all profiel names.</param>
    [method: JsonConstructor]
    public class ProfileListResponse(string currentProfileName, string[] profiles) : IResponse
    {
        /// <summary>
        /// Gets the name of the current profile.
        /// </summary>
        [JsonPropertyName("currentProfileName")]
        public string CurrentProfileName { get; } = currentProfileName;

        /// <summary>
        /// Gets the list of all profile names.
        /// </summary>
        [JsonPropertyName("profiles")]
        public string[] Profiles { get; } = profiles ?? [];
    }
}
