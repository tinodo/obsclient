namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;

    public class SceneItemRequestData
    {
        [JsonPropertyName("sceneName")]
        public string SceneName { get; set; }

        [JsonPropertyName("sceneItemId")]
        public int SceneItemId { get; set; }
        
        [JsonConstructor]
        public SceneItemRequestData(string sceneName, int sceneItemId)
        {
            this.SceneName = sceneName;
            this.SceneItemId = sceneItemId;
        }
    }
}
