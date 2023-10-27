namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetSceneItemBlendMode request.
    /// </summary>
    public class SceneItemBlendModeResponse : IResponse
    {
        /// <summary>
        /// Gets the <see cref="BlendMode"/>.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("sceneItemBlendMode")]
        public BlendMode SceneItemBlendMode { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SceneItemBlendModeResponse"/> class.
        /// </summary>
        /// <param name="sceneItemBlendMode">The <see cref="BlendMode"/>.</param>
        [JsonConstructor]
        public SceneItemBlendModeResponse(BlendMode sceneItemBlendMode)
        {
            this.SceneItemBlendMode = sceneItemBlendMode;
        }
    }
}
