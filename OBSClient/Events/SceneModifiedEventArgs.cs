namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    public class SceneModifiedEventArgs : EventArgs
    {
        [JsonPropertyName("sceneName")]
        public string SceneName { get; }

        [JsonPropertyName("isGroup")]
        public bool IsGroup { get; }

        [JsonConstructor]
        public SceneModifiedEventArgs(string sceneName, bool isGroup)
        {
            this.SceneName = sceneName;
            this.IsGroup = isGroup;
        }
    }
}
