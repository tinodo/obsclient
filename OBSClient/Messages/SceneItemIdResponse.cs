namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after successfully sending any of the following requests:
    /// CreateInput, GetSceneItemId, CreateSceneItem and DuplicateSceneItem
    /// </summary>
    public class SceneItemIdResponse : IResponse
    {
        /// <summary>
        /// Get the identifier of the scene item.
        /// </summary>
        [JsonPropertyName("sceneItemId")]
        public int SceneItemId { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SceneItemIdResponse"/> class.
        /// </summary>
        /// <param name="sceneItemId">The identifier of the scene item.</param>
        [JsonConstructor]
        public SceneItemIdResponse(int sceneItemId)
        {
            this.SceneItemId = sceneItemId;
        }
    }
}
