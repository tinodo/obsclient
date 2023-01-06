namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the SceneNameChanged event.
    /// </summary>
    public class SceneNameChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the old scene name.
        /// </summary>
        [JsonPropertyName("oldSceneName")]
        public string OldSceneName { get; }

        /// <summary>
        /// Gets the new scene name.
        /// </summary>
        [JsonPropertyName("sceneName")]
        public string SceneName { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SceneNameChangedEventArgs"/> class.
        /// </summary>
        /// <param name="oldSceneName">The old scene name.</param>
        /// <param name="sceneName">The new scene name.</param>
        [JsonConstructor]
        public SceneNameChangedEventArgs(string oldSceneName, string sceneName)
        {
            this.OldSceneName = oldSceneName;
            this.SceneName = sceneName;
        }
    }
}
