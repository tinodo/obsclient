namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class CurrentPreviewSceneNameResponseData : IResponseData
    {
        [JsonPropertyName("currentPreviewSceneName")]
        public string CurrentPreviewSceneName { get; set; }

        public CurrentPreviewSceneNameResponseData(string currentPreviewSceneName)
        {
            this.CurrentPreviewSceneName = currentPreviewSceneName;
        }
    }
}
