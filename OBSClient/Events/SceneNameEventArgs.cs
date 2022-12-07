namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    public class SceneNameEventArgs : EventArgs
    {
        [JsonPropertyName("sceneName")]
        public string SceneName { get; }

        [JsonConstructor]
        public SceneNameEventArgs(string sceneName)
        {
            this.SceneName = sceneName;
        }
    }
}
