namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the SceneCollectionList event.
    /// </summary>
    public class SceneCollectionListEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the scene collections.
        /// </summary>
        [JsonPropertyName("sceneCollections")]
        public string[] SceneCollections { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SceneCollectionListEventArgs"/> class.
        /// </summary>
        /// <param name="sceneCollections">The list of scene collections.</param>
        [JsonConstructor]
        public SceneCollectionListEventArgs(string[] sceneCollections)
        {
            this.SceneCollections = sceneCollections ?? Array.Empty<string>();
        }
    }
}
