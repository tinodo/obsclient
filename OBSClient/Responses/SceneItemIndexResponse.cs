namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetSceneItemIndex request.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="SceneItemIndexResponse"/> class.
    /// </remarks>
    /// <param name="sceneItemIndex">The index of the scene item.</param>
    [method: JsonConstructor]
    public class SceneItemIndexResponse(int sceneItemIndex) : IResponse
    {
        /// <summary>
        /// Gets the index of the scene item.
        /// </summary>
        [JsonPropertyName("sceneItemIndex")]
        public int SceneItemIndex { get; } = sceneItemIndex;
    }
}
