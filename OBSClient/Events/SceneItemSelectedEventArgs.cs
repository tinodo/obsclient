namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the SceneItemSelected event.
    /// </summary>
    public class SceneItemSelectedEventArgs : EventArgs
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
        /// Initializes a new instance of the <see cref="SceneItemSelectedEventArgs"/> class.
        /// </summary>
        /// <param name="sceneName">The scene name.</param>
        /// <param name="sceneItemId">The scene item id.</param>
        [JsonConstructor]
        public SceneItemSelectedEventArgs(string sceneName, int sceneItemId)
        {
            this.SceneName = sceneName;
            this.SceneItemId = sceneItemId;
        }
    }
}
