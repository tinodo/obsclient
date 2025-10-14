namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the SceneItemSelected event.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="SceneItemSelectedEventArgs"/> class.
    /// </remarks>
    /// <param name="sceneName">The scene name.</param>
    /// <param name="sceneUuid">The scene uuid.</param>
    /// <param name="sceneItemId">The scene item id.</param>
    [method: JsonConstructor]
    public class SceneItemSelectedEventArgs(string sceneName, Guid sceneUuid, int sceneItemId) : EventArgs
    {
        /// <summary>
        /// Gets the scene name.
        /// </summary>
        [JsonPropertyName("sceneName")]
        public string SceneName { get; } = sceneName;

        /// <summary>
        /// Gets the UUID of the scene.
        /// </summary>
        [JsonPropertyName("sceneUuid")]
        public Guid SceneUuid { get; } = sceneUuid;

        /// <summary>
        /// Gets the scene item id.
        /// </summary>
        [JsonPropertyName("sceneItemId")]
        public int SceneItemId { get; } = sceneItemId;
    }
}
