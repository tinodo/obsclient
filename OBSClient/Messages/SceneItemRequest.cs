namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Request Data for OBS Client Batch requests that require a sceneName and a sceneItemId.
    /// </summary>
    public class SceneItemRequest
    {
        /// <summary>
        /// Gets or sets the scene name.
        /// </summary>
        [JsonPropertyName("sceneName")]
        public string SceneName { get; set; }

        /// <summary>
        /// Gets or sets the scene item id.
        /// </summary>
        [JsonPropertyName("sceneItemId")]
        public int SceneItemId { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SceneItemRequest"/> class.
        /// </summary>
        /// <param name="sceneName">The scene name.</param>
        /// <param name="sceneItemId">The scene item id.</param>
        [JsonConstructor]
        public SceneItemRequest(string sceneName, int sceneItemId)
        {
            this.SceneName = sceneName;
            this.SceneItemId = sceneItemId;
        }
    }
}
