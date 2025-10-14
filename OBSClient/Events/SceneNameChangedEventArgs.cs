namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the SceneNameChanged event.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="SceneNameChangedEventArgs"/> class.
    /// </remarks>
    /// <param name="sceneUuid">The new scene uuid.</param>
    /// <param name="oldSceneName">The old scene name.</param>
    /// <param name="sceneName">The new scene name.</param>
    [method: JsonConstructor]
    public class SceneNameChangedEventArgs(Guid sceneUuid, string oldSceneName, string sceneName) : EventArgs
    {
        /// <summary>
        /// Gets the scene uuid.
        /// </summary>
        [JsonPropertyName("sceneUuid")]
        public Guid SceneUuid { get; } = sceneUuid;

        /// <summary>
        /// Gets the old scene name.
        /// </summary>
        [JsonPropertyName("oldSceneName")]
        public string OldSceneName { get; } = oldSceneName;

        /// <summary>
        /// Gets the new scene name.
        /// </summary>
        [JsonPropertyName("sceneName")]
        public string SceneName { get; } = sceneName;
    }
}
