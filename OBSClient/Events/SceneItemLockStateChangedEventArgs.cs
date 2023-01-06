namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the SceneItemLockStateChanged event.
    /// </summary>
    public class SceneItemLockStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the scene name.
        /// </summary>
        [JsonPropertyName("sceneName")]
        public string SceneName { get; }

        /// <summary>
        /// Gets the scene item id.
        /// </summary>
        [JsonPropertyName("sceneItemId")]
        public int SceneItemId { get; }

        /// <summary>
        /// Gets a value indicating whether the scene item is locked.
        /// </summary>
        [JsonPropertyName("sceneItemLocked")]
        public bool SceneItemLocked { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SceneItemLockStateChangedEventArgs"/> class.
        /// </summary>
        /// <param name="sceneName">The scene name.</param>
        /// <param name="sceneItemId">The scene item id.</param>
        /// <param name="sceneItemLocked">A value indicating whether the scene item is locked.</param>
        [JsonConstructor]
        public SceneItemLockStateChangedEventArgs(string sceneName, int sceneItemId, bool sceneItemLocked)
        {
            this.SceneName = sceneName;
            this.SceneItemId = sceneItemId;
            this.SceneItemLocked = sceneItemLocked;
        }
    }
}
