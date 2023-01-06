namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the CurrentProgramSceneChanged and CurrentPreviewSceneChanged events.
    /// </summary>
    public class SceneNameEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the scene name.
        /// </summary>
        [JsonPropertyName("sceneName")]
        public string SceneName { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SceneNameEventArgs"/> class.
        /// </summary>
        /// <param name="sceneName">The scene name.</param>
        [JsonConstructor]
        public SceneNameEventArgs(string sceneName)
        {
            this.SceneName = sceneName;
        }
    }
}
