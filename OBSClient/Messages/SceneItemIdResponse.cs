namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class SceneItemIdResponse : IResponse
    {
        [JsonPropertyName("sceneItemId")]
        public int SceneItemId { get; set; }

        [JsonConstructor]
        public SceneItemIdResponse(int sceneItemId)
        {
            this.SceneItemId = sceneItemId;
        }
    }
}
