namespace OBSStudioClient.Classes
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides a class for a Scene.
    /// </summary>
    public class Scene
    {
        /// <summary>
        /// Gets the scene index.
        /// </summary>
        [JsonPropertyName("sceneIndex")]
        public int SceneIndex { get; }

        /// <summary>
        /// Gets the scene name.
        /// </summary>
        [JsonPropertyName("sceneName")]
        public string SceneName { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Scene"/> class.
        /// </summary>
        /// <param name="sceneIndex">The scene index.</param>
        /// <param name="sceneName">The scene name.</param>
        [JsonConstructor]
        public Scene(int sceneIndex, string sceneName)
        {
            this.SceneIndex = sceneIndex;
            this.SceneName = sceneName;
        }
    }
}
