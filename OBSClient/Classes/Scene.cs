namespace OBSStudioClient.Classes
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides a class for a Scene.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="Scene"/> class.
    /// </remarks>
    /// <param name="sceneIndex">The scene index.</param>
    /// <param name="sceneName">The scene name.</param>
    [method: JsonConstructor]
    public class Scene(int sceneIndex, string sceneName)
    {
        /// <summary>
        /// Gets the scene index.
        /// </summary>
        [JsonPropertyName("sceneIndex")]
        public int SceneIndex { get; } = sceneIndex;

        /// <summary>
        /// Gets the scene name.
        /// </summary>
        [JsonPropertyName("sceneName")]
        public string SceneName { get; } = sceneName;
    }
}
