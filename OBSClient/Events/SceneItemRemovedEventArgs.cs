namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the SceneItemRemoved event.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="SceneItemRemovedEventArgs"/> class.
    /// </remarks>
    /// <param name="sceneName">The scene name.</param>
    /// <param name="sceneUuid">The scene uuid.</param>
    /// <param name="sourceName">The source name.</param>
    /// <param name="sourceUuid">The source uuid.</param>
    /// <param name="sceneItemId">The scene item id.</param>
    [method: JsonConstructor]
    public class SceneItemRemovedEventArgs(string sceneName, Guid sceneUuid, string sourceName, Guid sourceUuid, int sceneItemId) : EventArgs
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
        /// Gets the source name.
        /// </summary>
        [JsonPropertyName("sourceName")]
        public string SourceName { get; } = sourceName;

        /// <summary>
        /// Gets the UUID of the source.
        /// </summary>
        [JsonPropertyName("sourceUuid")]
        public Guid SourceUuid { get; } = sourceUuid;

        /// <summary>
        /// Gets the scene item id.
        /// </summary>
        [JsonPropertyName("sceneItemId")]
        public int SceneItemId { get; } = sceneItemId;
    }
}
