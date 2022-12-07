namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class CurrentPreviewSceneNameResponse : IResponse
    {
        [JsonPropertyName("currentPreviewSceneName")]
        public string CurrentPreviewSceneName { get; set; }

        public CurrentPreviewSceneNameResponse(string currentPreviewSceneName)
        {
            this.CurrentPreviewSceneName = currentPreviewSceneName;
        }
    }
}
