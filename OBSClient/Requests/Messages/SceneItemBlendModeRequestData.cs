namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Enums;
    using System.Text.Json.Serialization;

    public class SceneItemBlendModeRequestData
    {
        [JsonPropertyName("sceneName")]
        public string SceneName { get; set; }

        [JsonPropertyName("sceneItemId")]
        public int SceneItemId { get; set; }

        [JsonPropertyName("sceneItemBlendMode")]
        public BlendMode SceneItemBlendMode { get; set; }

        [JsonConstructor]
        public SceneItemBlendModeRequestData(string sceneName, int sceneItemId, BlendMode sceneItemBlendMode)
        {
            this.SceneName = sceneName;
            this.SceneItemId = sceneItemId;
            this.SceneItemBlendMode = sceneItemBlendMode;
        }
    }
}
