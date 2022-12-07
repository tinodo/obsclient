namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class CurrentProgramSceneNameResponse : IResponse
    {
        [JsonPropertyName("currentProgramSceneName")]
        public string CurrentProgramSceneName { get; set; }

        public CurrentProgramSceneNameResponse(string currentProgramSceneName)
        {
            this.CurrentProgramSceneName = currentProgramSceneName;
        }
    }
}
