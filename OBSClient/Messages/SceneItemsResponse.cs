namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Classes;
    using OBSStudioClient.Interfaces;

    public class SceneItemsResponse : IResponse
    {
        [JsonPropertyName("sceneItems")]
        public SceneItem[] SceneItems { get; set; }

        [JsonConstructor]
        public SceneItemsResponse(SceneItem[] sceneItems)
        {
            this.SceneItems = sceneItems ?? Array.Empty<SceneItem>();
        }
    }
}
