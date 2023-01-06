namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the SceneCreated and SceneRemoved events.
    /// </summary>
    public class SceneModifiedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the scene name.
        /// </summary>
        [JsonPropertyName("sceneName")]
        public string SceneName { get; }

        /// <summary>
        /// Gets a value indicating whether the scene is a group.
        /// </summary>
        [JsonPropertyName("isGroup")]
        public bool IsGroup { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SceneModifiedEventArgs"/> class.
        /// </summary>
        /// <param name="sceneName">The scene name.</param>
        /// <param name="isGroup">A value indicating whether the scene is a group.</param>
        [JsonConstructor]
        public SceneModifiedEventArgs(string sceneName, bool isGroup)
        {
            this.SceneName = sceneName;
            this.IsGroup = isGroup;
        }
    }
}
