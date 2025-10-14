namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful CreateScene request.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="CurrentPreviewSceneNameResponse"/> class.
    /// </remarks>
    /// <param name="sceneUuid">The Uuid of the preview scene.</param>
    public class CreateSceneResponse(Guid sceneUuid) : IResponse
    {
        /// <summary>
        /// Gets the uuid of the created scene.
        /// </summary>
        [JsonPropertyName("sceneUuid")]
        public Guid SceneUuid { get; } = sceneUuid;
    }
}
