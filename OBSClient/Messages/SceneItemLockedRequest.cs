namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;

    public class SceneItemLockedRequest
    {
        [JsonPropertyName("sceneName")]
        public string SceneName { get; set; }

        [JsonPropertyName("sceneItemId")]
        public int SceneItemId { get; set; }

        [JsonPropertyName("sceneItemLocked")]
        public bool SceneItemLocked { get; set; }

        [JsonConstructor]
        public SceneItemLockedRequest(string sceneName, int sceneItemId, bool sceneItemLocked)
        {
            this.SceneName = sceneName;
            this.SceneItemId = sceneItemId;
            this.SceneItemLocked = sceneItemLocked;
        }
    }
}
