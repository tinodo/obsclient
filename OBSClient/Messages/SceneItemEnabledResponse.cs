namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetSceneItemEnabled request.
    /// </summary>
    public class SceneItemEnabledResponse : IResponse
    {
        /// <summary>
        /// Gets a value indicating whether the scene item as been enabled.
        /// </summary>
        [JsonPropertyName("sceneItemEnabled")]
        public bool SceneItemEnabled { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SceneItemEnabledResponse"/> class.
        /// </summary>
        /// <param name="sceneItemEnabled">A value indicating whether the scene item as been enabled.</param>
        [JsonConstructor]
        public SceneItemEnabledResponse(bool sceneItemEnabled)
        {
            this.SceneItemEnabled = sceneItemEnabled;
        }
    }
}
