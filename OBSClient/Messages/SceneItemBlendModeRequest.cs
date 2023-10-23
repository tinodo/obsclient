namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Enums;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Request Data for OBS Client Batch requests that require a sceneName, sceneItemId and a sceneItemBlendMode.
    /// </summary>
    public class SceneItemBlendModeRequest : SceneItemRequest
    {
        /// <summary>
        /// Gets or sets the <see cref="BlendMode"/>.
        /// </summary>
        [JsonPropertyName("sceneItemBlendMode")]
        public BlendMode SceneItemBlendMode { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SceneItemBlendModeRequest"/> class.
        /// </summary>
        /// <param name="sceneName">The scene name.</param>
        /// <param name="sceneItemId">The scene item id.</param>
        /// <param name="sceneItemBlendMode">The <see cref="BlendMode"/>.</param>
        [JsonConstructor]
        public SceneItemBlendModeRequest(string sceneName, int sceneItemId, BlendMode sceneItemBlendMode) : base(sceneName, sceneItemId)
        {
            this.SceneItemBlendMode = sceneItemBlendMode;
        }
    }
}
