namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the CurrentProgramSceneChanged and CurrentPreviewSceneChanged events.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="SceneNameEventArgs"/> class.
    /// </remarks>
    /// <param name="sceneName">The scene name.</param>
    /// <param name="sceneUuid">The scene uuid.</param>
    [method: JsonConstructor]
    public class SceneNameEventArgs(string sceneName, Guid sceneUuid) : EventArgs
    {
        /// <summary>
        /// Gets the scene name.
        /// </summary>
        [JsonPropertyName("sceneName")]
        public string SceneName { get; } = sceneName;

        /// <summary>
        /// Gets the scene uuid.
        /// </summary>
        [JsonPropertyName("sceneUuid")]
        public Guid SceneUuid { get; } = sceneUuid;
    }
}
