namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;

    public class SetSceneNameRequest
    {
        [JsonPropertyName("sceneName")]
        public string SceneName { get; set; }

        [JsonPropertyName("newSceneName")]
        public string NewSceneName { get; set; }

        [JsonConstructor]
        public SetSceneNameRequest(string sceneName, string newSceneName)
        {
            this.SceneName = sceneName;
            this.NewSceneName = newSceneName;
        }
    }
}
