namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Classes;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Request Data for OBS Client Batch requests that require a sceneName, sceneItemId and sceneItemTransform.
    /// </summary>
    public class SceneItemTransformRequest : SceneItemRequest
    {
        /// <summary>
        /// Gets or sets the <see cref="SceneItemTransform"/>."/>
        /// </summary>
        [JsonPropertyName("sceneItemTransform")]
        public SceneItemTransform SceneItemTransform { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SceneItemTransformRequest"/> class.
        /// </summary>
        /// <param name="sceneName">The scene name.</param>
        /// <param name="sceneItemId">The scene id.</param>
        /// <param name="sceneItemTransform">The <see cref="SceneItemTransform"/>.</param>
        [JsonConstructor]
        public SceneItemTransformRequest(string sceneName, int sceneItemId, SceneItemTransform sceneItemTransform) : base(sceneName, sceneItemId)
        {
            this.SceneItemTransform = sceneItemTransform;
        }
    }
}
