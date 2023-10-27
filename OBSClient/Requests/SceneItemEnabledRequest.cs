namespace OBSStudioClient.Requests
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Request Data for OBS Client Batch requests that require a sceneName, sceneItemId and sceneItemEnabled.
    /// </summary>
    public class SceneItemEnabledRequest : SceneItemRequest
    {
        /// <summary>
        /// Get or sets the scene item enabled setting.
        /// </summary>
        [JsonPropertyName("sceneItemEnabled")]
        public bool SceneItemEnabled { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SceneItemEnabledRequest"/> class.
        /// </summary>
        /// <param name="sceneName">The scene name.</param>
        /// <param name="sceneItemId">The scene item id.</param>
        /// <param name="sceneItemEnabled">The scne item enabled value.</param>
        [JsonConstructor]
        public SceneItemEnabledRequest(string sceneName, int sceneItemId, bool sceneItemEnabled) : base(sceneName, sceneItemId)
        {
            this.SceneItemEnabled = sceneItemEnabled;
        }
    }
}
