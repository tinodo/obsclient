namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    public class SceneItemEnableStateChangedEventArgs : EventArgs
    {
        [JsonPropertyName("sceneName")]
        public string SceneName { get; }

        [JsonPropertyName("sceneItemId")]
        public int SceneItemId { get; }

        [JsonPropertyName("sceneItemEnabled")]
        public bool SceneItemEnabled { get; }

        [JsonConstructor]
        public SceneItemEnableStateChangedEventArgs(string sceneName, int sceneItemId, bool sceneItemEnabled)
        {
            this.SceneName = sceneName;
            this.SceneItemId = sceneItemId;
            this.SceneItemEnabled = sceneItemEnabled;
        }
    }
}
