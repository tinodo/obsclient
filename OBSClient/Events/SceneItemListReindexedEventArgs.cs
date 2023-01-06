namespace OBSStudioClient.Events
{
    using OBSStudioClient.Classes;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the SceneItemListReindexed event.
    /// </summary>
    public class SceneItemListReindexedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the scene name.
        /// </summary>
        [JsonPropertyName("sceneName")]
        public string SceneName { get; }

        /// <summary>
        /// Gets the list of <see cref="SceneItem"/>.
        /// </summary>
        [JsonPropertyName("sceneItems")]
        public SceneItem[] SceneItems { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SceneItemListReindexedEventArgs"/> class.
        /// </summary>
        /// <param name="sceneName">The scene name.</param>
        /// <param name="sceneItems">The lits of <see cref="SceneItem"/>.</param>
        [JsonConstructor]
        public SceneItemListReindexedEventArgs(string sceneName, SceneItem[] sceneItems)
        {
            this.SceneName = sceneName;
            this.SceneItems = sceneItems ?? Array.Empty<SceneItem>();
        }
    }
}
