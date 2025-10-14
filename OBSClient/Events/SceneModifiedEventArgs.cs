namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the SceneCreated and SceneRemoved events.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="SceneModifiedEventArgs"/> class.
    /// </remarks>
    /// <param name="sceneName">The scene name.</param>
    /// <param name="sceneUuid">The scene uuid.</param>
    /// <param name="isGroup">A value indicating whether the scene is a group.</param>
    [method: JsonConstructor]
    public class SceneModifiedEventArgs(string sceneName, Guid sceneUuid, bool isGroup) : EventArgs
    {
        /// <summary>
        /// Gets the scene name.
        /// </summary>
        [JsonPropertyName("sceneName")]
        public string SceneName { get; } = sceneName;

        /// <summary>
        /// Gets the scene uuid.
        /// </summary>
        [JsonPropertyName("sceneUuid")]
        public Guid SceneUuid { get; } = sceneUuid;

        /// <summary>
        /// Gets a value indicating whether the scene is a group.
        /// </summary>
        [JsonPropertyName("isGroup")]
        public bool IsGroup { get; } = isGroup;
    }
}
