namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class SceneItemlockedResponse : IResponse
    {
        [JsonPropertyName("sceneItemLocked")]
        public bool SceneItemLocked { get; set; }

        [JsonConstructor]
        public SceneItemlockedResponse(bool sceneItemLocked)
        {
            this.SceneItemLocked = sceneItemLocked;
        }
    }
}
