namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Classes;
    using OBSStudioClient.Interfaces;

    public class SceneItemTransformResponseData : IResponseData
    {
        [JsonPropertyName("sceneItemTransform")]
        public SceneItemTransform SceneItemTransform { get; set; }

        [JsonConstructor]
        public SceneItemTransformResponseData(SceneItemTransform sceneItemTransform)
        {
            this.SceneItemTransform = sceneItemTransform;
        }
    }
}
