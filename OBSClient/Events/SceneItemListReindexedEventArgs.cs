namespace OBSStudioClient.Events
{
    using OBSStudioClient.Classes;
    using System.Text.Json.Serialization;

    public class SceneItemListReindexedEventArgs : EventArgs
    {
        [JsonPropertyName("sceneName")]
        public string SceneName { get; }

        [JsonPropertyName("sceneItems")]
        public SceneItem[] SceneItems { get; }

        [JsonConstructor]
        public SceneItemListReindexedEventArgs(string sceneName, SceneItem[] sceneItems)
        {
            this.SceneName = sceneName;
            this.SceneItems = sceneItems ?? Array.Empty<SceneItem>();
        }
    }
}
