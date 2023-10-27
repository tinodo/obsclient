namespace OBSStudioClient.Requests
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Duplicate Scene Request Class
    /// </summary>
    public class DuplicateSceneRequest
    {
        /// <summary>
        /// Name of the Scene to duplicate.
        /// </summary>
        [JsonPropertyName("sceneName")]
        public string SceneName { get; set; }

        /// <summary>
        /// Id of the Scene Item
        /// </summary>
        [JsonPropertyName("sceneItemId")]
        public int SceneItemId { get; set; }

        /// <summary>
        /// Name of the Scene to create.
        /// </summary>
        [JsonPropertyName("destinationSceneName")]
        public string? DestinationSceneName { get; set; }

        /// <summary>
        /// Creates a new instance of a <see cref="DuplicateSceneRequest"/> object.
        /// </summary>
        /// <param name="sceneName">Name of the source scene.</param>
        /// <param name="sceneItemId">The Scene Item identifier</param>
        /// <param name="destinationSceneName">Name of the scene to create.</param>
        [JsonConstructor]
        public DuplicateSceneRequest(string sceneName, int sceneItemId, string? destinationSceneName)
        {
            this.SceneName = sceneName;
            this.SceneItemId = sceneItemId;
            this.DestinationSceneName = destinationSceneName;
        }
    }
}
