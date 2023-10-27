namespace OBSStudioClient.Requests
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Request Data for OBS Client Batch requests that require a sceneName and a newSceneName.
    /// </summary>
    public class SetSceneNameRequest
    {
        /// <summary>
        /// Gets or sets the scene name.
        /// </summary>
        [JsonPropertyName("sceneName")]
        public string SceneName { get; set; }

        /// <summary>
        /// Gets or sets the new scene name.
        /// </summary>
        [JsonPropertyName("newSceneName")]
        public string NewSceneName { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SetSceneNameRequest"/> class.
        /// </summary>
        /// <param name="sceneName">The scene name.</param>
        /// <param name="newSceneName">The new scene name.</param>
        [JsonConstructor]
        public SetSceneNameRequest(string sceneName, string newSceneName)
        {
            this.SceneName = sceneName;
            this.NewSceneName = newSceneName;
        }
    }
}
