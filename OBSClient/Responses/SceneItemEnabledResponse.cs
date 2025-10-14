namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetSceneItemEnabled request.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="SceneItemEnabledResponse"/> class.
    /// </remarks>
    /// <param name="sceneItemEnabled">A value indicating whether the scene item as been enabled.</param>
    [method: JsonConstructor]
    public class SceneItemEnabledResponse(bool sceneItemEnabled) : IResponse
    {
        /// <summary>
        /// Gets a value indicating whether the scene item as been enabled.
        /// </summary>
        [JsonPropertyName("sceneItemEnabled")]
        public bool SceneItemEnabled { get; } = sceneItemEnabled;
    }
}
