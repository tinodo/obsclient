namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the SceneCollectionList event.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="SceneCollectionListEventArgs"/> class.
    /// </remarks>
    /// <param name="sceneCollections">The list of scene collections.</param>
    [method: JsonConstructor]
    public class SceneCollectionListEventArgs(string[] sceneCollections) : EventArgs
    {
        /// <summary>
        /// Gets the scene collections.
        /// </summary>
        [JsonPropertyName("sceneCollections")]
        public string[] SceneCollections { get; } = sceneCollections ?? [];
    }
}
