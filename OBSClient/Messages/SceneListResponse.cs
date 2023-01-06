namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetSceneList request.
    /// </summary>
    public class SceneListResponse : IResponse
    {
        /// <summary>
        /// Gets the current program scene name.
        /// </summary>
        [JsonPropertyName("currentProgramSceneName")]
        public string CurrentProgramSceneName { get; }

        /// <summary>
        /// Gets the current preview scene name in Studio Mode.
        /// </summary>
        [JsonPropertyName("currentPreviewSceneName")]
        public string? CurrentPreviewSceneName { get; }

        /// <summary>
        /// Gets a list of <see cref="Scene"/>.
        /// </summary>
        [JsonPropertyName("scenes")]
        public Scene[] Scenes { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SceneListResponse"/> class.
        /// </summary>
        /// <param name="currentProgramSceneName">The current program scene name.</param>
        /// <param name="currentPreviewSceneName">The current preview scene name in Studio Mode.</param>
        /// <param name="scenes">A list of <see cref="Scene"/>.</param>
        [JsonConstructor]
        public SceneListResponse(string currentProgramSceneName, string? currentPreviewSceneName, Scene[] scenes)
        {
            this.CurrentProgramSceneName = currentProgramSceneName;
            this.CurrentPreviewSceneName = currentPreviewSceneName;
            this.Scenes = scenes;
        }
    }
}
