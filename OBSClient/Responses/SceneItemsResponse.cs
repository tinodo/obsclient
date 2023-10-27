namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetSceneItemList or GetGroupSceneItemList request.
    /// </summary>
    public class SceneItemsResponse : IResponse
    {
        /// <summary>
        /// Gets a list of <see cref="SceneItem"/>.
        /// </summary>
        [JsonPropertyName("sceneItems")]
        public SceneItem[] SceneItems { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SceneItemsResponse"/> class.
        /// </summary>
        /// <param name="sceneItems">A list of <see cref="SceneItem"/>.</param>
        [JsonConstructor]
        public SceneItemsResponse(SceneItem[] sceneItems)
        {
            this.SceneItems = sceneItems ?? Array.Empty<SceneItem>();
        }
    }
}
