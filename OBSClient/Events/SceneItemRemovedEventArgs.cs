namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    public class SceneItemRemovedEventArgs : EventArgs
    {
        [JsonPropertyName("sceneName")]
        public string SceneName { get; }

        [JsonPropertyName("sourceName")]
        public string SourceName { get; }

        [JsonPropertyName("sceneItemId")]
        public int SceneItemId { get; }

        [JsonConstructor]
        public SceneItemRemovedEventArgs(string sceneName, string sourceName, int sceneItemId)
        {
            this.SceneName = sceneName;
            this.SourceName = sourceName;
            this.SceneItemId = sceneItemId;
        }
    }
}
