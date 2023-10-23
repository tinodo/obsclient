namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Request Data for OBS Client Batch requests that require a sceneName, sceneItemId and sceneItemIndex.
    /// </summary>
    public class SceneItemIndexRequest : SceneItemRequest
    {
        /// <summary>
        /// Gets or sets the scene item index.
        /// </summary>
        [JsonPropertyName("sceneItemIndex")]
        public int SceneItemIndex { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SceneItemIndexRequest"/> class.
        /// </summary>
        /// <param name="sceneName">The scene name.</param>
        /// <param name="sceneItemId">The scene item id.</param>
        /// <param name="sceneItemIndex">The scene item index.</param>
        [JsonConstructor]
        public SceneItemIndexRequest(string sceneName, int sceneItemId, int sceneItemIndex) : base (sceneName, sceneItemId)
        {
            this.SceneItemIndex = sceneItemIndex;
        }
    }
}
