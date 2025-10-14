namespace OBSStudioClient.Events
{
    using OBSStudioClient.Classes;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the SceneItemTransformChanged event.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="SceneItemTransformChangedEventArgs"/> class.
    /// </remarks>
    /// <param name="sceneName">The scene name.</param>
    /// <param name="sceneUuid">The scene uuid.</param>
    /// <param name="sceneItemId">The scene item id.</param>
    /// <param name="sceneItemTransform">The <see cref="SceneItemTransform"/>.</param>
    [method: JsonConstructor]
    public class SceneItemTransformChangedEventArgs(string sceneName, Guid sceneUuid, int sceneItemId, SceneItemTransform sceneItemTransform) : EventArgs
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

        /// <summary>
        /// Gets the <see cref="SceneItemTransform"/>.
        /// </summary>
        [JsonPropertyName("sceneItemTransform")]
        public SceneItemTransform SceneItemTransform { get; } = sceneItemTransform;
    }
}
