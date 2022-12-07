namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class SceneItemEnabledResponse : IResponse
    {
        [JsonPropertyName("sceneItemEnabled")]
        public bool SceneItemEnabled { get; set; }

        [JsonConstructor]
        public SceneItemEnabledResponse(bool sceneItemEnabled)
        {
            this.SceneItemEnabled = sceneItemEnabled;
        }
    }
}
