namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;

    public class SceneNameRequest
    {
        [JsonPropertyName("sceneName")]
        public string SceneName { get; set; }

        [JsonConstructor]
        public SceneNameRequest(string sceneName)
        {
            this.SceneName = sceneName;
        }
    }
}
