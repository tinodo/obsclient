namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetSceneItemTransform request.
    /// </summary>
    public class SceneItemTransformResponse : IResponse
    {
        /// <summary>
        /// Gets the <see cref="SceneItemTransform"/>.
        /// </summary>
        [JsonPropertyName("sceneItemTransform")]
        public SceneItemTransform SceneItemTransform { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SceneItemTransformResponse"/> class.
        /// </summary>
        /// <param name="sceneItemTransform">The <see cref="SceneItemTransform"/>.</param>
        [JsonConstructor]
        public SceneItemTransformResponse(SceneItemTransform sceneItemTransform)
        {
            this.SceneItemTransform = sceneItemTransform;
        }
    }
}
