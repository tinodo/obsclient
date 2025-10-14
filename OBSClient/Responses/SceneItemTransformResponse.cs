namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetSceneItemTransform request.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="SceneItemTransformResponse"/> class.
    /// </remarks>
    /// <param name="sceneItemTransform">The <see cref="SceneItemTransform"/>.</param>
    [method: JsonConstructor]
    public class SceneItemTransformResponse(SceneItemTransform sceneItemTransform) : IResponse
    {
        /// <summary>
        /// Gets the <see cref="SceneItemTransform"/>.
        /// </summary>
        [JsonPropertyName("sceneItemTransform")]
        public SceneItemTransform SceneItemTransform { get; } = sceneItemTransform;
    }
}
