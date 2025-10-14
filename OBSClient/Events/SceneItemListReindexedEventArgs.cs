namespace OBSStudioClient.Events
{
    using OBSStudioClient.Classes;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the SceneItemListReindexed event.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="SceneItemListReindexedEventArgs"/> class.
    /// </remarks>
    /// <param name="sceneName">The scene name.</param>
    /// <param name="sceneUuid">The scene uuid.</param>
    /// <param name="sceneItems">The lits of <see cref="SceneItem"/>.</param>
    [method: JsonConstructor]
    public class SceneItemListReindexedEventArgs(string sceneName, Guid sceneUuid, SceneItem[] sceneItems) : EventArgs
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
        /// Gets the list of <see cref="SceneItem"/>.
        /// </summary>
        [JsonPropertyName("sceneItems")]
        public SceneItem[] SceneItems { get; } = sceneItems ?? [];
    }
}
