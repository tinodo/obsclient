namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;

    public class CreateSceneItemRequestData
    {
        [JsonPropertyName("sceneName")]
        public string SceneName { get; set; }

        [JsonPropertyName("sourceName")]
        public string SourceName { get; set; }


        [JsonPropertyName("sceneItemEnabled")]
        public bool? SceneItemEnabled { get; set; }

        [JsonConstructor]
        public CreateSceneItemRequestData(string sceneName, string sourceName, bool? sceneItemEnabled)
        {
            this.SceneName = sceneName;
            this.SourceName = sourceName;
            this.SceneItemEnabled = sceneItemEnabled;
        }
    }
}
