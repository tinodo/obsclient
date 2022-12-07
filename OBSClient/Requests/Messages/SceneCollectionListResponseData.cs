namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class SceneCollectionListResponseData: IResponseData
    {
        [JsonPropertyName("currentSceneCollectionName")]
        public string CurrentSceneCollectionName { get; set; }

        [JsonPropertyName("sceneCollections")]
        public string[] SceneCollections { get; set; }

        public SceneCollectionListResponseData(string currentSceneCollectionName, string[] sceneCollections)
        {
            this.CurrentSceneCollectionName = currentSceneCollectionName;
            this.SceneCollections = sceneCollections ?? Array.Empty<string>();
        }
    }
}
