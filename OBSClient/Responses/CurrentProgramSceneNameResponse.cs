namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetCurrentProgramScene request.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="CurrentProgramSceneNameResponse"/> class.
    /// </remarks>
    /// <param name="sceneName">The name of the program scene.</param>
    /// <param name="sceneUuid">The Uuid of the program scene.</param>
    public class CurrentProgramSceneNameResponse(string sceneName, Guid sceneUuid) : IResponse
    {
        /// <summary>
        /// Gets the name of the current active scene.
        /// </summary>
        [JsonPropertyName("sceneName")]
        public string SceneName { get; } = sceneName;

        /// <summary>
        /// Gets the uuid of the current active scene.
        /// </summary>
        [JsonPropertyName("sceneUuid")]
        public Guid SceneUuid { get; } = sceneUuid;

        /// <summary>
        /// Gets the name of the current active scene.
        /// </summary>
        [Obsolete("Use SceneName instead. This property will be removed in future versions.")]
        [JsonIgnore]
        public string CurrentProgramSceneName { get { return SceneName; } }

        /// <summary>
        /// Gets the uuid of the current active scene.
        /// </summary>
        [Obsolete("Use SceneUuid instead. This property will be removed in future versions.")]
        [JsonIgnore]
        public Guid CurrentProgramSceneUuid{ get { return SceneUuid; } }
    }
}
