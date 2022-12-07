namespace OBSStudioClient.Events
{
    using OBSStudioClient.Classes;
    using System.Text.Json.Serialization;

    public class SceneItemTransformChangedEventArgs : EventArgs
    {
        [JsonPropertyName("sceneName")]
        public string SceneName { get; }

        [JsonPropertyName("sceneItemId")]
        public int SceneItemId { get; }

        [JsonPropertyName("sceneItemTransform")]
        public SceneItemTransform SceneItemTransform { get; }

        [JsonConstructor]
        public SceneItemTransformChangedEventArgs(string sceneName, int sceneItemId, SceneItemTransform sceneItemTransform)
        {
            this.SceneName = sceneName;
            this.SceneItemId = sceneItemId;
            this.SceneItemTransform = sceneItemTransform;
        }
    }
}
