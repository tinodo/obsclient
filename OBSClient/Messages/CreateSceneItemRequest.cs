namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;

    public class CreateSceneItemRequest
    {
        [JsonPropertyName("sceneName")]
        public string SceneName { get; set; }

        [JsonPropertyName("sourceName")]
        public string SourceName { get; set; }


        [JsonPropertyName("sceneItemEnabled")]
        public bool? SceneItemEnabled { get; set; }

        [JsonConstructor]
        public CreateSceneItemRequest(string sceneName, string sourceName, bool? sceneItemEnabled)
        {
            this.SceneName = sceneName;
            this.SourceName = sourceName;
            this.SceneItemEnabled = sceneItemEnabled;
        }
    }
}
