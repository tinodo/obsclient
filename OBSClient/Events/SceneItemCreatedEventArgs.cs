namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    public class SceneItemCreatedEventArgs : EventArgs
    {
        [JsonPropertyName("sceneName")]
        public string SceneName { get; }

        [JsonPropertyName("sourceName")]
        public string SourceName { get; }

        [JsonPropertyName("sceneItemId")]
        public int SceneItemId { get; }

        [JsonPropertyName("sceneItemIndex")]
        public int SceneItemIndex { get; }

        [JsonConstructor]
        public SceneItemCreatedEventArgs(string sceneName, string sourceName, int sceneItemId, int sceneItemIndex)
        {
            this.SceneName = sceneName;
            this.SourceName = sourceName;
            this.SceneItemId = sceneItemId;
            this.SceneItemIndex = sceneItemIndex;
        }
    }
}
