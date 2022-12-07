namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;

    public class SceneItemIdRequestData
    {
        [JsonPropertyName("sceneName")]
        public string SceneName { get; set; }

        [JsonPropertyName("sourceName")]
        public string SourceName { get; set; }


        [JsonPropertyName("searchOffset")]
        public int? SearchOffset { get; set; }

        [JsonConstructor]
        public SceneItemIdRequestData(string sceneName, string sourceName, int? searchOffset)
        {
            this.SceneName = sceneName;
            this.SourceName = sourceName;
            this.SearchOffset = searchOffset;
        }
    }
}
