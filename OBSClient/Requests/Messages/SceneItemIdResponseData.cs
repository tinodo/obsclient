namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class SceneItemIdResponseData : IResponseData
    {
        [JsonPropertyName("sceneItemId")]
        public int SceneItemId { get; set; }

        [JsonConstructor]
        public SceneItemIdResponseData(int sceneItemId)
        {
            this.SceneItemId = sceneItemId;
        }
    }
}
