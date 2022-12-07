namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    public class SceneItemBlendModeResponse : IResponse
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("sceneItemBlendMode")]
        public BlendMode SceneItemBlendMode { get; set; }

        [JsonConstructor]
        public SceneItemBlendModeResponse(BlendMode sceneItemBlendMode)
        {
            this.SceneItemBlendMode = sceneItemBlendMode;
        }
    }
}
