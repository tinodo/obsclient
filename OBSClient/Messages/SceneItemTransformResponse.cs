namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Classes;
    using OBSStudioClient.Interfaces;

    public class SceneItemTransformResponse : IResponse
    {
        [JsonPropertyName("sceneItemTransform")]
        public SceneItemTransform SceneItemTransform { get; set; }

        [JsonConstructor]
        public SceneItemTransformResponse(SceneItemTransform sceneItemTransform)
        {
            this.SceneItemTransform = sceneItemTransform;
        }
    }
}
