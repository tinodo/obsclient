namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetSceneList request.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="SceneListResponse"/> class.
    /// </remarks>
    /// <param name="currentProgramSceneName">The current program scene name.</param>
    /// <param name="currentProgramSceneUuid">The current program scene uuid.</param>
    /// <param name="currentPreviewSceneName">The current preview scene name in Studio Mode.</param>
    /// <param name="currentPreviewSceneUuid">The current preview scene uuid in Studio Mode.</param>
    /// <param name="scenes">A list of <see cref="Scene"/>.</param>
    [method: JsonConstructor]
    public class SceneListResponse(string? currentProgramSceneName, Guid? currentProgramSceneUuid, string? currentPreviewSceneName, Guid? currentPreviewSceneUuid, Scene[] scenes) : IResponse
    {
        /// <summary>
        /// Gets the current program scene name.
        /// </summary>
        [JsonPropertyName("currentProgramSceneName")]
        public string? CurrentProgramSceneName { get; } = currentProgramSceneName;

        /// <summary>
        /// Gets the current program scene name.
        /// </summary>
        [JsonPropertyName("currentProgramSceneUuid")]
        public Guid? CurrentProgramSceneUuid { get; } = currentProgramSceneUuid;

        /// <summary>
        /// Gets the current preview scene name in Studio Mode.
        /// </summary>
        [JsonPropertyName("currentPreviewSceneName")]
        public string? CurrentPreviewSceneName { get; } = currentPreviewSceneName;

        /// <summary>
        /// Gets the current preview scene name in Studio Mode.
        /// </summary>
        [JsonPropertyName("currentPreviewSceneUuid")]
        public Guid? CurrentPreviewSceneUuid { get; } = currentPreviewSceneUuid;

        /// <summary>
        /// Gets a list of <see cref="Scene"/>.
        /// </summary>
        [JsonPropertyName("scenes")]
        public Scene[] Scenes { get; } = scenes;
    }
}
