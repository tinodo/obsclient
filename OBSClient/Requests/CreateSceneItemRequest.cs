namespace OBSStudioClient.Requests
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// The <see cref="CreateSceneItemRequest"/> object holds the Request Data in the Request Message (<see cref="IMessage"/>) for a CreateSceneItem request.
    /// </summary>
    public class CreateSceneItemRequest
    {
        /// <summary>
        /// Name of the Scene.
        /// </summary>
        [JsonPropertyName("sceneName")]
        public string SceneName { get; set; }

        /// <summary>
        /// Name of the Source.
        /// </summary>
        [JsonPropertyName("sourceName")]
        public string SourceName { get; set; }

        /// <summary>
        /// Whether the Scene Item is Enabled or not.
        /// </summary>
        [JsonPropertyName("sceneItemEnabled")]
        public bool? SceneItemEnabled { get; set; }

        /// <summary>
        /// Creates a new instance of a <see cref="SceneItemRequest"/> object.
        /// </summary>
        /// <param name="sceneName">Scene name</param>
        /// <param name="sourceName">Source name</param>
        /// <param name="sceneItemEnabled">Whether the scene item is enabled</param>
        [JsonConstructor]
        public CreateSceneItemRequest(string sceneName, string sourceName, bool? sceneItemEnabled)
        {
            this.SceneName = sceneName;
            this.SourceName = sourceName;
            this.SceneItemEnabled = sceneItemEnabled;
        }
    }
}
