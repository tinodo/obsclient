namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    public class SceneCollectionNameEventArgs : EventArgs
    {
        [JsonPropertyName("sceneCollectionName")]
        public string SceneCollectionName { get; }

        [JsonConstructor]
        public SceneCollectionNameEventArgs(string sceneCollectionName)
        {
            this.SceneCollectionName = sceneCollectionName;
        }
    }
}
