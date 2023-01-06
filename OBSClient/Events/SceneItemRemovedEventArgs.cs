namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the SceneItemRemoved event.
    /// </summary>
    public class SceneItemRemovedEventArgs : EventArgs
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
        /// Initializes a new instance of the <see cref="SceneItemRemovedEventArgs"/> class.
        /// </summary>
        /// <param name="sceneName">The scene name.</param>
        /// <param name="sourceName">The source name.</param>
        /// <param name="sceneItemId">The scene item id.</param>
        [JsonConstructor]
        public SceneItemRemovedEventArgs(string sceneName, string sourceName, int sceneItemId)
        {
            this.SceneName = sceneName;
            this.SourceName = sourceName;
            this.SceneItemId = sceneItemId;
        }
    }
}
