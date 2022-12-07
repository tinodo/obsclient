namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;

    public class DuplicateSceneRequest
    {
        [JsonPropertyName("sceneName")]
        public string SceneName { get; set; }

        [JsonPropertyName("sceneItemId")]
        public int SceneItemId { get; set; }

        [JsonPropertyName("destinationSceneName")]
        public string? DestinationSceneName { get; set; }

        [JsonConstructor]
        public DuplicateSceneRequest(string sceneName, int sceneItemId, string? destinationSceneName)
        {
            this.SceneName = sceneName;
            this.SceneItemId = sceneItemId;
            this.DestinationSceneName = destinationSceneName;
        }
    }
}
