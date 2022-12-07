namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Classes;
    using OBSStudioClient.Interfaces;

    public class SceneTransitionListResponseData : IResponseData
    {
        [JsonPropertyName("currentSceneTransitionName")]
        public string CurrentSceneTransitionName { get;set; }

        [JsonPropertyName("currentSceneTransitionKind")]
        public string CurrentSceneTransitionKind { get; set; }

        [JsonPropertyName("transitions")]
        public Transition[] Transitions { get; set; }

        public SceneTransitionListResponseData(string currentSceneTransitionName, string currentSceneTransitionKind, Transition[] transitions)
        {
            this.CurrentSceneTransitionName = currentSceneTransitionName;
            this.CurrentSceneTransitionKind = currentSceneTransitionKind;
            this.Transitions = transitions;
        }
    }
}
