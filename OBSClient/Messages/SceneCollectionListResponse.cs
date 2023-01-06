namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetSceneCollectionList request.
    /// </summary>
    public class SceneCollectionListResponse : IResponse
    {
        /// <summary>
        /// Gets the name of the current scene collection.
        /// </summary>
        [JsonPropertyName("currentSceneCollectionName")]
        public string CurrentSceneCollectionName { get; }

        /// <summary>
        /// Gets the list of all scene collection names.
        /// </summary>
        [JsonPropertyName("sceneCollections")]
        public string[] SceneCollections { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SceneCollectionListResponse"/> class.
        /// </summary>
        /// <param name="currentSceneCollectionName">The name of the current scene collection.</param>
        /// <param name="sceneCollections">The list of all scene collection names.</param>
        public SceneCollectionListResponse(string currentSceneCollectionName, string[] sceneCollections)
        {
            this.CurrentSceneCollectionName = currentSceneCollectionName;
            this.SceneCollections = sceneCollections ?? Array.Empty<string>();
        }
    }
}
