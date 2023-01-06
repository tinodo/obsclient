namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the SceneItemEnableStateChanged event.
    /// </summary>
    public class SceneItemEnableStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the name of the scene.
        /// </summary>
        [JsonPropertyName("sceneName")]
        public string SceneName { get; }

        /// <summary>
        /// Gets the scene item id.
        /// </summary>
        [JsonPropertyName("sceneItemId")]
        public int SceneItemId { get; }

        /// <summary>
        /// Gets a value indicating whether the scene item is enabled.
        /// </summary>
        [JsonPropertyName("sceneItemEnabled")]
        public bool SceneItemEnabled { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SceneItemEnableStateChangedEventArgs"/> class.
        /// </summary>
        /// <param name="sceneName">The scene name.</param>
        /// <param name="sceneItemId">The scene item id.</param>
        /// <param name="sceneItemEnabled">A value indicating whether the scene item is enabled.</param>
        [JsonConstructor]
        public SceneItemEnableStateChangedEventArgs(string sceneName, int sceneItemId, bool sceneItemEnabled)
        {
            this.SceneName = sceneName;
            this.SceneItemId = sceneItemId;
            this.SceneItemEnabled = sceneItemEnabled;
        }
    }
}
