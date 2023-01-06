namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the SceneItemCreated event.
    /// </summary>
    public class SceneItemCreatedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the scene name.
        /// </summary>
        [JsonPropertyName("sceneName")]
        public string SceneName { get; }

        /// <summary>
        /// Gets the source name.
        /// </summary>
        [JsonPropertyName("sourceName")]
        public string SourceName { get; }

        /// <summary>
        /// Gets the scene item id.
        /// </summary>
        [JsonPropertyName("sceneItemId")]
        public int SceneItemId { get; }

        /// <summary>
        /// Gets the scene item index.
        /// </summary>
        [JsonPropertyName("sceneItemIndex")]
        public int SceneItemIndex { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SceneItemCreatedEventArgs"/> class.
        /// </summary>
        /// <param name="sceneName">The scene name.</param>
        /// <param name="sourceName">The source name.</param>
        /// <param name="sceneItemId">The scene item id.</param>
        /// <param name="sceneItemIndex">The scene item index.</param>
        [JsonConstructor]
        public SceneItemCreatedEventArgs(string sceneName, string sourceName, int sceneItemId, int sceneItemIndex)
        {
            this.SceneName = sceneName;
            this.SourceName = sourceName;
            this.SceneItemId = sceneItemId;
            this.SceneItemIndex = sceneItemIndex;
        }
    }
}
