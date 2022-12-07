namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    public class SceneCollectionListEventArgs : EventArgs
    {
        [JsonPropertyName("sceneCollections")]
        public string[] SceneCollections { get; }

        [JsonConstructor]
        public SceneCollectionListEventArgs(string[] sceneCollections)
        {
            this.SceneCollections = sceneCollections ?? Array.Empty<string>();
        }
    }
}
