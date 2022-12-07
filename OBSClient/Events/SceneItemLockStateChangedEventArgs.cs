namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    public class SceneItemLockStateChangedEventArgs : EventArgs
    {
        [JsonPropertyName("sceneName")]
        public string SceneName { get; }

        [JsonPropertyName("sceneItemId")]
        public int SceneItemId { get; }

        [JsonPropertyName("sceneItemLocked")]
        public bool SceneItemLocked { get; }

        [JsonConstructor]
        public SceneItemLockStateChangedEventArgs(string sceneName, int sceneItemId, bool sceneItemLocked)
        {
            this.SceneName = sceneName;
            this.SceneItemId = sceneItemId;
            this.SceneItemLocked = sceneItemLocked;
        }
    }
}
