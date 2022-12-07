namespace OBSStudioClient.Classes
{
    using System.Text.Json.Serialization;

    public class Scene
    {
        [JsonPropertyName("sceneIndex")]
        public int SceneIndex { get; }

        [JsonPropertyName("sceneName")]
        public string SceneName { get; }

        [JsonConstructor]
        public Scene(int sceneIndex, string sceneName)
        {
            SceneIndex = sceneIndex;
            SceneName = sceneName;
        }
    }
}
