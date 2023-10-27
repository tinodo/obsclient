namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetSceneItemLocked request.
    /// </summary>
    public class SceneItemlockedResponse : IResponse
    {
        /// <summary>
        /// Gets a value indicating whether the scene item is locked.
        /// </summary>
        [JsonPropertyName("sceneItemLocked")]
        public bool SceneItemLocked { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SceneItemlockedResponse"/> class.
        /// </summary>
        /// <param name="sceneItemLocked">A value indicating whether the scene item is locked.</param>
        [JsonConstructor]
        public SceneItemlockedResponse(bool sceneItemLocked)
        {
            this.SceneItemLocked = sceneItemLocked;
        }
    }
}
