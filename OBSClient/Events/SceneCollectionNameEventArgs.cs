namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the CurrentSceneCollectionChanging and CurrentSceneCollectionChanged events.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="SceneCollectionNameEventArgs"/> class.
    /// </remarks>
    /// <param name="sceneCollectionName">The name of the scene collection.</param>
    [method: JsonConstructor]
    public class SceneCollectionNameEventArgs(string sceneCollectionName) : EventArgs
    {
        /// <summary>
        /// Gets the name of the scene collection.
        /// </summary>
        [JsonPropertyName("sceneCollectionName")]
        public string SceneCollectionName { get; } = sceneCollectionName;
    }
}
