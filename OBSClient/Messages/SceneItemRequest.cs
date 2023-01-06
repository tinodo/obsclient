namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;

    public class SceneItemRequest
    {
        [JsonPropertyName("sceneName")]
        public string SceneName { get; set; }

        [JsonPropertyName("sceneItemId")]
        public int SceneItemId { get; set; }

        [JsonConstructor]
        public SceneItemRequest(string sceneName, int sceneItemId)
        {
            this.SceneName = sceneName;
            this.SceneItemId = sceneItemId;
        }
    }
}
