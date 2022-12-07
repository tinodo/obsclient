namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class SceneItemEnabledResponseData : IResponseData
    {
        [JsonPropertyName("sceneItemEnabled")]
        public bool SceneItemEnabled { get; set; }

        [JsonConstructor]
        public SceneItemEnabledResponseData(bool sceneItemEnabled)
        {
            this.SceneItemEnabled = sceneItemEnabled;
        }
    }
}
