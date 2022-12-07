namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;

    public class SetSceneNameRequestData
    {
        [JsonPropertyName("sceneName")]
        public string SceneName { get; set; }

        [JsonPropertyName("newSceneName")]
        public string NewSceneName { get; set; }

        [JsonConstructor]
        public SetSceneNameRequestData(string sceneName, string newSceneName)
        {
            this.SceneName = sceneName;
            this.NewSceneName = newSceneName;
        }
    }
}
