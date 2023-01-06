namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the CurrentSceneCollectionChanging and CurrentSceneCollectionChanged events.
    /// </summary>
    public class SceneCollectionNameEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the name of the scene collection.
        /// </summary>
        [JsonPropertyName("sceneCollectionName")]
        public string SceneCollectionName { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SceneCollectionNameEventArgs"/> class.
        /// </summary>
        /// <param name="sceneCollectionName">The name of the scene collection.</param>
        [JsonConstructor]
        public SceneCollectionNameEventArgs(string sceneCollectionName)
        {
            this.SceneCollectionName = sceneCollectionName;
        }
    }
}
