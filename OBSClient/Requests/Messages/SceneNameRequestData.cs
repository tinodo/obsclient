namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;

    public class SceneNameRequestData
    {
        [JsonPropertyName("sceneName")]
        public string SceneName { get; set; }

        [JsonConstructor]
        public SceneNameRequestData(string sceneName)
        {
            this.SceneName = sceneName;
        }
    }
}
