namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the CurrentProfileChanging and CurrentProfileChanged events.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="ProfileNameEventArgs"/> class.
    /// </remarks>
    /// <param name="profileName">The profile name.</param>
    [method: JsonConstructor]
    public class ProfileNameEventArgs(string profileName) : EventArgs
    {
        /// <summary>
        /// Gets the profile name.
        /// </summary>
        [JsonPropertyName("profileName")]
        public string ProfileName { get; } = profileName;
    }
}
