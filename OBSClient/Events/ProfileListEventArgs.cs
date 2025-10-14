namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the ProfileListChanged event.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="ProfileListEventArgs"/> class.
    /// </remarks>
    /// <param name="profiles">The list of profiles.</param>
    /// <summary>
    /// Provides arguments for the ProfileListChanged event.
    /// </summary>
    [method: JsonConstructor]
    public class ProfileListEventArgs(string[] profiles) : EventArgs
    {
        /// <summary>
        /// Gets the list of profiles.
        /// </summary>
        [JsonPropertyName("profiles")]
        public string[] Profiles { get; } = profiles ?? [];
    }
}
