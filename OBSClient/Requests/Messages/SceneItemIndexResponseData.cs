namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class SceneItemIndexResponseData : IResponseData
    {
        [JsonPropertyName("sceneItemIndex")]
        public int SceneItemIndex { get; set; }

        [JsonConstructor]
        public SceneItemIndexResponseData(int sceneItemIndex)
        {
            this.SceneItemIndex = sceneItemIndex;
        }
    }
}
