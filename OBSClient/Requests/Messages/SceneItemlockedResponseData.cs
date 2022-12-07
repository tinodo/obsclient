namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class SceneItemlockedResponseData : IResponseData
    {
        [JsonPropertyName("sceneItemLocked")]
        public bool SceneItemLocked { get; set; }

        [JsonConstructor]
        public SceneItemlockedResponseData(bool sceneItemLocked)
        {
            this.SceneItemLocked = sceneItemLocked;
        }
    }
}
