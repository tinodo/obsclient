namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class SceneItemIndexResponse : IResponse
    {
        [JsonPropertyName("sceneItemIndex")]
        public int SceneItemIndex { get; set; }

        [JsonConstructor]
        public SceneItemIndexResponse(int sceneItemIndex)
        {
            this.SceneItemIndex = sceneItemIndex;
        }
    }
}
