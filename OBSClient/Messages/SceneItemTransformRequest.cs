namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Classes;
    using System.Text.Json.Serialization;

    public class SceneItemTransformRequest
    {
        [JsonPropertyName("sceneName")]
        public string SceneName { get; set; }

        [JsonPropertyName("sceneItemId")]
        public int SceneItemId { get; set; }

        [JsonPropertyName("sceneItemTransform")]
        public SceneItemTransform SceneItemTransform { get; set; }

        [JsonConstructor]
        public SceneItemTransformRequest(string sceneName, int sceneItemId, SceneItemTransform sceneItemTransform)
        {
            this.SceneName = sceneName;
            this.SceneItemId = sceneItemId;
            this.SceneItemTransform = sceneItemTransform;
        }
    }
}
