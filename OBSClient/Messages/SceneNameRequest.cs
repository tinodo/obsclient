namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Request Data for OBS Client Batch requests that require a sceneName.
    /// </summary>
    public class SceneNameRequest
    {
        /// <summary>
        /// Gets or sets the scene name.
        /// </summary>
        [JsonPropertyName("sceneName")]
        public string SceneName { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SceneNameRequest"/> class.
        /// </summary>
        /// <param name="sceneName">The scene name.</param>
        [JsonConstructor]
        public SceneNameRequest(string sceneName)
        {
            this.SceneName = sceneName;
        }
    }
}
