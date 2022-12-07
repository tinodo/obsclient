namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Classes;
    using OBSStudioClient.Interfaces;

    public class SceneItemsResponseData : IResponseData
    {
        [JsonPropertyName("sceneItems")]
        public SceneItem[] SceneItems { get; set; }

        [JsonConstructor]
        public SceneItemsResponseData(SceneItem[] sceneItems)
        {
            this.SceneItems = sceneItems ?? Array.Empty<SceneItem>();
        }
    }
}
