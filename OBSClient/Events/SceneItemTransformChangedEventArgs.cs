namespace OBSStudioClient.Events
{
    using OBSStudioClient.Classes;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the SceneItemTransformChanged event.
    /// </summary>
    public class SceneItemTransformChangedEventArgs : EventArgs
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
        /// Gets the <see cref="SceneItemTransform"/>.
        /// </summary>
        [JsonPropertyName("sceneItemTransform")]
        public SceneItemTransform SceneItemTransform { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SceneItemTransformChangedEventArgs"/> class.
        /// </summary>
        /// <param name="sceneName">The scene name.</param>
        /// <param name="sceneItemId">The scene item id.</param>
        /// <param name="sceneItemTransform">The <see cref="SceneItemTransform"/>.</param>
        [JsonConstructor]
        public SceneItemTransformChangedEventArgs(string sceneName, int sceneItemId, SceneItemTransform sceneItemTransform)
        {
            this.SceneName = sceneName;
            this.SceneItemId = sceneItemId;
            this.SceneItemTransform = sceneItemTransform;
        }
    }
}
