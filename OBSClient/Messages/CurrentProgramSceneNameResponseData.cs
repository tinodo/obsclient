namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class CurrentProgramSceneNameResponseData : IResponseData
    {
        [JsonPropertyName("currentProgramSceneName")]
        public string CurrentProgramSceneName { get; set; }

        public CurrentProgramSceneNameResponseData(string currentProgramSceneName)
        {
            this.CurrentProgramSceneName = currentProgramSceneName;
        }
    }
}
