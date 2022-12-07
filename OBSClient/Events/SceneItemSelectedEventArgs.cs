namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    public class SceneItemSelectedEventArgs : EventArgs
    {
        [JsonPropertyName("sceneName")]
        public string SceneName { get; }

        [JsonPropertyName("sceneItemId")]
        public int SceneItemId { get; }

        [JsonConstructor]
        public SceneItemSelectedEventArgs(string sceneName, int sceneItemId)
        {
            this.SceneName = sceneName;
            this.SceneItemId = sceneItemId;
        }
    }
}
