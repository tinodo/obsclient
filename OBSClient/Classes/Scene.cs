namespace OBSStudioClient.Classes
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides a class for a Scene.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="Scene"/> class.
    /// </remarks>
    /// <param name="sceneIndex">The scene index. <see langword="null"/> when the scene belongs to a non-main canvas, which has no ordered scene index.</param>
    /// <param name="sceneName">The scene name.</param>
    /// <param name="sceneUuid">The scene uuid.</param>
    [method: JsonConstructor]
    public class Scene(int? sceneIndex, string sceneName, Guid sceneUuid)
    {
        /// <summary>
        /// Gets the scene index. <see langword="null"/> when the scene belongs to a non-main canvas.
        /// </summary>
        [JsonPropertyName("sceneIndex")]
        public int? SceneIndex { get; } = sceneIndex;

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

