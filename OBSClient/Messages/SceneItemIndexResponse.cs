namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetSceneItemIndex request.
    /// </summary>
    public class SceneItemIndexResponse : IResponse
    {
        /// <summary>
        /// Gets the index of the scene item.
        /// </summary>
        [JsonPropertyName("sceneItemIndex")]
        public int SceneItemIndex { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SceneItemIndexResponse"/> class.
        /// </summary>
        /// <param name="sceneItemIndex">The index of the scene item.</param>
        [JsonConstructor]
        public SceneItemIndexResponse(int sceneItemIndex)
        {
            this.SceneItemIndex = sceneItemIndex;
        }
    }
}
