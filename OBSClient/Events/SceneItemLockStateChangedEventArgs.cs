namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the SceneItemLockStateChanged event.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="SceneItemLockStateChangedEventArgs"/> class.
    /// </remarks>
    /// <param name="sceneName">The scene name.</param>
    /// <param name="sceneUuid">The scene uuid.</param>
    /// <param name="sceneItemId">The scene item id.</param>
    /// <param name="sceneItemLocked">A value indicating whether the scene item is locked.</param>
    [method: JsonConstructor]
    public class SceneItemLockStateChangedEventArgs(string sceneName, Guid sceneUuid, int sceneItemId, bool sceneItemLocked) : EventArgs
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
        /// Gets a value indicating whether the scene item is locked.
        /// </summary>
        [JsonPropertyName("sceneItemLocked")]
        public bool SceneItemLocked { get; } = sceneItemLocked;
    }
}
