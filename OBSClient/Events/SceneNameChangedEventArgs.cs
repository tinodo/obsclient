namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    public class SceneNameChangedEventArgs : EventArgs
    {
        [JsonPropertyName("oldSceneName")]
        public string OldSceneName { get; }

        [JsonPropertyName("sceneName")]
        public string SceneName { get; }

        [JsonConstructor]
        public SceneNameChangedEventArgs(string oldSceneName, string sceneName)
        {
            this.OldSceneName = oldSceneName;
            this.SceneName = sceneName;
        }
    }
}
