namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Request Data for OBS Client Batch requests that require a sceneName, sceneItemId and sceneItemLocked.
    /// </summary>
    public class SceneItemLockedRequest : SceneItemRequest
    {
        /// <summary>
        /// Gets or sets a value indicating whether the scene item is locked.
        /// </summary>
        [JsonPropertyName("sceneItemLocked")]
        public bool SceneItemLocked { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SceneItemLockedRequest"/> class.
        /// </summary>
        /// <param name="sceneName">The scene name.</param>
        /// <param name="sceneItemId">The scene item id.</param>
        /// <param name="sceneItemLocked">A value indicating whether the scene item is locked.</param>
        [JsonConstructor]
        public SceneItemLockedRequest(string sceneName, int sceneItemId, bool sceneItemLocked) : base(sceneName, sceneItemId)
        {
            this.SceneItemLocked = sceneItemLocked;
        }
    }
}
